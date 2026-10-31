namespace Statevia.Core.Application.Scheduling;

/// <summary>
/// Container 実行と同じ実効 Action タイムアウト。
/// </summary>
/// <remarks>
/// <para><see cref="SandboxTimeoutSeconds"/> が無いときは <see cref="DockerDefaultTimeoutSeconds"/>。</para>
/// <para>許容は 10〜3600 秒。範囲外は補正しない。</para>
/// </remarks>
/// <param name="SandboxTimeoutSeconds"><c>Statevia:ExecutionPolicy:Sandbox:TimeoutSeconds</c>。未設定は null。</param>
/// <param name="DockerDefaultTimeoutSeconds"><c>Statevia:ExecutionPolicy:Sandbox:Docker:DefaultTimeoutSeconds</c>。未設定時のプラットフォーム既定は 60。</param>
public sealed record EffectiveActionTimeoutSettings(int? SandboxTimeoutSeconds, int DockerDefaultTimeoutSeconds)
{
    /// <summary>設定が無いときの既定（Docker 既定 60 秒）。</summary>
    public static EffectiveActionTimeoutSettings PlatformDefault { get; } = new(null, DefaultDockerSeconds);

    /// <summary>秒数の下限。</summary>
    public const int MinSeconds = 10;

    /// <summary>秒数の上限。</summary>
    public const int MaxSeconds = 3_600;

    /// <summary>Docker 既定タイムアウト（秒）。</summary>
    public const int DefaultDockerSeconds = 60;

    /// <summary>実効秒数が許容内なら <paramref name="timeout"/> に入れる。</summary>
    /// <param name="timeout">解決した時間。</param>
    /// <returns>許容内なら true。</returns>
    public bool TryResolve(out TimeSpan timeout)
    {
        var seconds = SandboxTimeoutSeconds ?? DockerDefaultTimeoutSeconds;
        if (seconds is < MinSeconds or > MaxSeconds)
        {
            timeout = default;
            return false;
        }

        timeout = TimeSpan.FromSeconds(seconds);
        return true;
    }
}
