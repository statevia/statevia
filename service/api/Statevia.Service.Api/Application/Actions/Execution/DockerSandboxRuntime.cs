using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Infrastructure.Modules;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>Docker 上の短命 Action Host コンテナで Action を実行する <see cref="IActionSandboxRuntime"/>。</summary>
/// <remarks>
/// <para>ProviderKey は <c>docker</c>。呼び出し単位で Create→gRPC Execute→Remove する。</para>
/// <para>
/// v1 の <see cref="DockerSandboxOptions.ActionRuntimeProfile"/> は <c>dotnet-8.0</c> のみ。
/// Engine 内部状態は渡さず、リクエストと入力のみを Host へ委譲する。
/// </para>
/// </remarks>
/// <param name="options">実行ポリシー（Sandbox.Docker 含む）。</param>
/// <param name="docker">Docker クライアント境界。</param>
/// <param name="hostExecutor">一時 BaseUrl 向け Host 実行。</param>
/// <param name="tenantKeys">実行テナントの tenant_key。</param>
/// <param name="logger">構造化ログ（機密を含めない）。</param>
internal sealed class DockerSandboxRuntime(
    IOptions<ExecutionPolicyOptions> options,
    IDockerContainerClient docker,
    IEphemeralActionHostExecutor hostExecutor,
    ISandboxTenantKeyLookup tenantKeys,
    ILogger<DockerSandboxRuntime> logger) : IActionSandboxRuntime
{
    private const string SandboxRuntimeUnavailableCode = "SandboxRuntimeUnavailable";
    private const string SandboxTenantModulesUnavailableCode = "SandboxTenantModulesUnavailable";
    private const string TenantModulesUnavailableMessage = "Tenant modules directory is unavailable.";

    /// <inheritdoc />
    public string ProviderKey => "docker";

    /// <inheritdoc />
    public async Task<ActionExecutionResult> RunAsync(
        ActionExecutionRequest request,
        SandboxLimits limits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(limits);

        var dockerOptions = options.Value.Sandbox.Docker;
        if (!IsSupportedProfile(dockerOptions.ActionRuntimeProfile))
        {
            return Failure(
                SandboxRuntimeUnavailableCode,
                $"Unsupported ActionRuntimeProfile '{dockerOptions.ActionRuntimeProfile}'. v1 allows '{DockerSandboxOptions.DefaultActionRuntimeProfile}' only.");
        }

        if (string.IsNullOrWhiteSpace(dockerOptions.Image))
        {
            return Failure(SandboxRuntimeUnavailableCode, "Docker sandbox image is not configured.");
        }

        // 起動検証（分類 A）と揃えて Trim 後に判定する（Options.Create 経由の防御）。
        if (string.Equals(dockerOptions.NetworkMode?.Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                SandboxRuntimeUnavailableCode,
                "Docker NetworkMode 'none' is not supported in v1 because host-to-container gRPC requires connectivity.");
        }

        // DefaultTimeoutSeconds / GrpcPort は分類 A（ValidateOnStart）。範囲外は補正せず fail-safe。
        if (!IsValidDefaultTimeoutSeconds(dockerOptions.DefaultTimeoutSeconds))
        {
            return Failure(
                SandboxRuntimeUnavailableCode,
                "Docker DefaultTimeoutSeconds is out of the allowed range.");
        }

        if (!IsValidGrpcPort(dockerOptions.GrpcPort))
        {
            return Failure(
                SandboxRuntimeUnavailableCode,
                "Docker GrpcPort is out of the allowed range.");
        }

        var timeout = limits.Timeout ?? TimeSpan.FromSeconds(dockerOptions.DefaultTimeoutSeconds);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        // NetworkMode / ModulesContainerPath の空白のみ分類 B（Warning＋既定値）。
        var networkMode = ResolveNetworkMode(dockerOptions.NetworkMode);
        var modulesContainerPath = ResolveModulesContainerPath(dockerOptions.ModulesContainerPath);
        var grpcPort = dockerOptions.GrpcPort;

        DockerStartedContainer? started = null;
        try
        {
            var mount = await ResolveTenantModulesMountAsync(
                request,
                dockerOptions.ModulesHostPath,
                timeoutCts.Token).ConfigureAwait(false);
            if (mount.RejectedResult is not null)
            {
                return mount.RejectedResult;
            }

            started = await docker.StartActionHostContainerAsync(
                new DockerContainerStartRequest(
                    dockerOptions.Image.Trim(),
                    networkMode,
                    limits.CpuLimit,
                    limits.MemoryLimitMiB,
                    mount.HostPath,
                    modulesContainerPath,
                    grpcPort),
                timeoutCts.Token).ConfigureAwait(false);

            DockerSandboxRuntimeLog.ContainerStarted(logger, started.ContainerId);
            return await ExecuteWithStartupRetryAsync(started.BaseUrl, request, timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("SandboxTimeout", "Docker sandbox execution timed out.");
        }
        catch (OperationCanceledException)
        {
            return Failure("Cancelled", "Docker sandbox execution was cancelled.");
        }
#pragma warning disable CA1031 // サンドボックス障害はホストを落とさず Failed に正規化する
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            DockerSandboxRuntimeLog.ContainerFailed(logger, ex);
            return Failure(SandboxRuntimeUnavailableCode, "Docker sandbox runtime is unavailable.");
        }
        finally
        {
            if (started is not null)
            {
                try
                {
                    await docker.StopAndRemoveAsync(started.ContainerId, CancellationToken.None)
                        .ConfigureAwait(false);
                    DockerSandboxRuntimeLog.ContainerRemoved(logger, started.ContainerId);
                }
#pragma warning disable CA1031 // クリーンアップ失敗は実行結果に影響させない
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    DockerSandboxRuntimeLog.ContainerCleanupFailed(logger, ex, started.ContainerId);
                }
            }
        }
    }

    /// <summary>
    /// <paramref name="configuredModulesHostPath"/> が無いときはマウントしない。
    /// あるときは実行テナントのディレクトリだけを返す。解決できないときは Docker を呼ばない。
    /// </summary>
    private async Task<TenantModulesMount> ResolveTenantModulesMountAsync(
        ActionExecutionRequest request,
        string? configuredModulesHostPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuredModulesHostPath))
        {
            return new TenantModulesMount(null, null);
        }

        if (!Guid.TryParse(request.TenantId, out var tenantId))
        {
            DockerSandboxRuntimeLog.TenantModulesMountRejected(logger);
            return new TenantModulesMount(null, Failure(SandboxTenantModulesUnavailableCode, TenantModulesUnavailableMessage));
        }

        string? tenantKey;
        try
        {
            tenantKey = await tenantKeys.FindTenantKeyAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(tenantKey))
            {
                var hostPath = TenantModulePath.ResolveTenantModulesRoot(configuredModulesHostPath.Trim(), tenantKey);
                return new TenantModulesMount(hostPath, null);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // 解決失敗はホストを落とさず、その Action の失敗にする。例外本文は秘密を含み得る
        catch (Exception)
#pragma warning restore CA1031
        {
            DockerSandboxRuntimeLog.TenantModulesMountRejected(logger);
            return new TenantModulesMount(null, Failure(SandboxTenantModulesUnavailableCode, TenantModulesUnavailableMessage));
        }

        DockerSandboxRuntimeLog.TenantModulesMountRejected(logger);
        return new TenantModulesMount(null, Failure(SandboxTenantModulesUnavailableCode, TenantModulesUnavailableMessage));
    }

    private readonly record struct TenantModulesMount(string? HostPath, ActionExecutionResult? RejectedResult);

    private static bool IsValidDefaultTimeoutSeconds(int configuredSeconds) =>
        configuredSeconds is >= DockerSandboxOptions.MinDefaultTimeoutSeconds
            and <= DockerSandboxOptions.MaxDefaultTimeoutSeconds;

    private static bool IsValidGrpcPort(int configured) =>
        configured is >= DockerSandboxOptions.MinGrpcPort and <= DockerSandboxOptions.MaxGrpcPort;

    private string ResolveNetworkMode(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        DockerSandboxRuntimeLog.OptionsFallbackApplied(logger, "NetworkMode");
        return "bridge";
    }

    private string ResolveModulesContainerPath(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        DockerSandboxRuntimeLog.OptionsFallbackApplied(logger, "ModulesContainerPath");
        return DockerSandboxOptions.DefaultModulesContainerPath;
    }

    private static bool IsSupportedProfile(string? profile) =>
        string.Equals(
            string.IsNullOrWhiteSpace(profile) ? DockerSandboxOptions.DefaultActionRuntimeProfile : profile.Trim(),
            DockerSandboxOptions.DefaultActionRuntimeProfile,
            StringComparison.Ordinal);

    /// <summary>
    /// コンテナ起動直後の一時的な gRPC 失敗を短時間リトライする。
    /// Docker Desktop はアプリ待受前にホストポートを公開し、HTTP/2 handshake 失敗になることがある。
    /// </summary>
    private async Task<ActionExecutionResult> ExecuteWithStartupRetryAsync(
        string baseUrl,
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 40;
        var attempt = 0;
        ActionExecutionResult result;
        do
        {
            attempt++;
            cancellationToken.ThrowIfCancellationRequested();
            result = await hostExecutor.ExecuteAsync(baseUrl, request, cancellationToken).ConfigureAwait(false);
            if (result.Success || !IsTransientStartupFailure(result))
            {
                return result;
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }
        while (attempt < maxAttempts);

        return result;
    }

    private static bool IsTransientStartupFailure(ActionExecutionResult result)
    {
        if (string.Equals(result.ErrorCode, "ActionHostUnavailable", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(result.ErrorCode, "ActionHostRpcFailed", StringComparison.Ordinal)
            && result.ErrorMessage is { } message
            && message.Contains("HTTP/2 handshake", StringComparison.OrdinalIgnoreCase);
    }

    private static ActionExecutionResult Failure(string errorCode, string message) =>
        new()
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
        };
}

/// <summary><see cref="DockerSandboxRuntime"/> の構造化ログ。</summary>
internal static partial class DockerSandboxRuntimeLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Started Docker sandbox container {ContainerId}")]
    public static partial void ContainerStarted(ILogger logger, string containerId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Removed Docker sandbox container {ContainerId}")]
    public static partial void ContainerRemoved(ILogger logger, string containerId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Docker sandbox execution failed")]
    public static partial void ContainerFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Failed to clean up Docker sandbox container {ContainerId}")]
    public static partial void ContainerCleanupFailed(ILogger logger, Exception exception, string containerId);

    /// <summary>
    /// 分類 B: 空白の Docker オプションを既定値へ補正したときの警告。
    /// 設定値そのものは出力しない（キー名のみ）。範囲外の数値は分類 A のためここでは扱わない。
    /// </summary>
    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Docker sandbox option {OptionKey} was empty; using configured default")]
    public static partial void OptionsFallbackApplied(ILogger logger, string optionKey);

    /// <summary>テナントディレクトリを解決できなかった。パスと秘密は出さない。</summary>
    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "Docker sandbox tenant modules mount was rejected")]
    public static partial void TenantModulesMountRejected(ILogger logger);
}
