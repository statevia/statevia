namespace Statevia.Service.ActionHost.Execution;

/// <summary>プロセス環境の名前比較。</summary>
internal static class ProcessEnvironmentNames
{
    /// <summary>OS の環境変数名の比較。Windows は大文字小文字を区別しない。</summary>
    internal static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
