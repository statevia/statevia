using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Statevia.Tools.Capacity;

/// <summary>Phase 0 の Service API コンテナを docker compose で再起動する。</summary>
/// <remarks>
/// D1 専用。引数は ASCII 識別子のみを許し、シェル経由の任意コマンドは実行しない。
/// </remarks>
internal static class ComposeRestart
{
    /// <summary>既定 compose の Service API サービス名。</summary>
    internal const string DefaultServiceName = "service-api";

    private static readonly Regex ServiceNamePattern = new("^[A-Za-z0-9][A-Za-z0-9_-]{0,62}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>compose に渡す引数（restart）。</summary>
    /// <param name="serviceName">Compose サービス名。</param>
    /// <returns><c>compose restart {service}</c>。</returns>
    public static IReadOnlyList<string> BuildRestartArguments(string serviceName)
    {
        return ["compose", "restart", NormalizeServiceName(serviceName)];
    }

    /// <summary>compose に渡す引数（version プローブ）。</summary>
    public static IReadOnlyList<string> BuildVersionArguments() => ["compose", "version"];

    /// <summary>サービス名を検証する。</summary>
    /// <param name="serviceName">Compose サービス名。</param>
    /// <returns>検証済みの名前。</returns>
    /// <exception cref="ArgumentException">識別子として不正。</exception>
    public static string NormalizeServiceName(string serviceName)
    {
        if (!ServiceNamePattern.IsMatch(serviceName))
        {
            throw new ArgumentException("Compose service name must be an ASCII identifier (letters, digits, hyphen, underscore).");
        }

        return serviceName;
    }

    /// <summary><c>docker compose version</c> で CLI の到達を確認する。</summary>
    /// <param name="workingDirectory">compose ファイルがあるディレクトリ（通常 git ルート）。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    public static Task ProbeAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        return RunDockerAsync(workingDirectory, BuildVersionArguments(), cancellationToken);
    }

    /// <summary>指定サービスを再起動する。</summary>
    /// <param name="workingDirectory">compose ファイルがあるディレクトリ。</param>
    /// <param name="serviceName">Compose サービス名。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    public static Task RestartAsync(string workingDirectory, string serviceName, CancellationToken cancellationToken)
    {
        return RunDockerAsync(workingDirectory, BuildRestartArguments(serviceName), cancellationToken);
    }

    private static async Task RunDockerAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"docker {string.Join(' ', arguments)} exited {process.ExitCode}.");
        }
    }
}
