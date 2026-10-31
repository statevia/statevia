using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>OutOfProcess の Action を、空いている Action Host へ送る。</summary>
/// <remarks>InProcess と Container はこのクライアントを使わない。エンドポイントが複数のときは 1 本のチャネルへ固定しない。</remarks>
internal sealed class GrpcActionHostExecutionClient : IActionHostExecutionClient, IDisposable
{
    private readonly ActionHostEndpointPool _pool;

    /// <summary>設定からエンドポイントごとのチャネルを開く。</summary>
    /// <param name="options">Action Host 接続設定。</param>
    public GrpcActionHostExecutionClient(IOptions<ActionHostClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        var calls = options.Value.ResolveEndpoints()
            .Select(static address => (IActionHostCall)new GrpcActionHostCall(address))
            .ToArray();
        _pool = new ActionHostEndpointPool(calls);
    }

    /// <summary>テスト用に呼び出し先を差し替える。</summary>
    /// <param name="calls">エンドポイントごとの呼び出し。空なら未設定。</param>
    internal GrpcActionHostExecutionClient(IReadOnlyList<IActionHostCall> calls)
    {
        ArgumentNullException.ThrowIfNull(calls);
        _pool = new ActionHostEndpointPool(calls);
    }

    /// <inheritdoc />
    public Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken) =>
        _pool.ExecuteAsync(request, cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _pool.Dispose();
}
