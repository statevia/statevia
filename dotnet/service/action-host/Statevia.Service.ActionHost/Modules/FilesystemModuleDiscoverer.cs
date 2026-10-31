namespace Statevia.Service.ActionHost.Modules;

/// <summary>modules ルートから entry DLL を列挙する（load は行わない）。</summary>
/// <remarks>
/// 直下はテナントディレクトリ、その子が Module ディレクトリである。データベースは見ない。
/// </remarks>
internal sealed class FilesystemModuleDiscoverer
{
    private readonly ILogger<FilesystemModuleDiscoverer> _logger;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="logger">ログ。</param>
    public FilesystemModuleDiscoverer(ILogger<FilesystemModuleDiscoverer> logger) =>
        _logger = logger;

    /// <summary>テナントディレクトリの子から Action Module を発見する。</summary>
    /// <param name="modulesRoot">modules ルート絶対パス。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>発見済み module 一覧。直下のテナントディレクトリ自体は含まない。</returns>
    /// <remarks>
    /// 配置は <c>{modulesRoot}/{tenantKey}/{moduleId}/</c>。入口 DLL は <see cref="TryResolveEntryAssemblyPath"/>。
    /// 子ディレクトリのさらに下（<c>bin</c> や <c>obj</c>）は見ない。入口パスが modules ルートの外なら発見しない。
    /// </remarks>
    public IReadOnlyList<DiscoveredModule> Discover(string modulesRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modulesRoot);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(modulesRoot))
        {
            FilesystemModuleDiscovererLog.ModulesRootMissing(_logger, modulesRoot);
            return Array.Empty<DiscoveredModule>();
        }

        var modulesRootFullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modulesRoot));
        var discovered = new List<DiscoveredModule>();
        foreach (var tenantDirectory in Directory.EnumerateDirectories(modulesRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiscoverUnderTenantDirectory(discovered, modulesRootFullPath, tenantDirectory, cancellationToken);
        }

        return discovered;
    }

    /// <summary>テナントディレクトリ直下の子だけを Module 候補にする。</summary>
    /// <param name="discovered">発見結果の追記先。</param>
    /// <param name="modulesRootFullPath">正規化済み modules ルート。</param>
    /// <param name="tenantDirectory">テナントディレクトリ。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    private void DiscoverUnderTenantDirectory(
        List<DiscoveredModule> discovered,
        string modulesRootFullPath,
        string tenantDirectory,
        CancellationToken cancellationToken)
    {
        var tenantDirectoryName = Path.GetFileName(tenantDirectory);
        foreach (var moduleDirectory in Directory.EnumerateDirectories(tenantDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var moduleDirectoryName = Path.GetFileName(moduleDirectory);
            if (string.IsNullOrWhiteSpace(moduleDirectoryName))
            {
                continue;
            }

            if (!TryResolveEntryAssemblyPath(moduleDirectory, moduleDirectoryName, out var entryAssemblyPath, out var reason)
                || !IsEntryInsideModulesRoot(modulesRootFullPath, entryAssemblyPath))
            {
                var skipReason = string.IsNullOrEmpty(reason)
                    ? "entry path is outside the modules root"
                    : reason;
                FilesystemModuleDiscovererLog.ModuleDirectorySkipped(
                    _logger,
                    $"{tenantDirectoryName}/{moduleDirectoryName}",
                    skipReason);
                continue;
            }

            discovered.Add(new DiscoveredModule(moduleDirectoryName, entryAssemblyPath));
        }
    }

    /// <summary>入口 DLL が modules ルート配下に収まるかを返す。</summary>
    /// <param name="modulesRootFullPath">正規化済み modules ルート。</param>
    /// <param name="entryAssemblyPath">解決済み入口 DLL。</param>
    /// <returns>ルート配下なら <see langword="true"/>。</returns>
    internal static bool IsEntryInsideModulesRoot(string modulesRootFullPath, string entryAssemblyPath)
    {
        var relative = Path.GetRelativePath(modulesRootFullPath, entryAssemblyPath);
        return !string.IsNullOrWhiteSpace(relative)
            && !relative.StartsWith("..", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    internal static bool TryResolveEntryAssemblyPath(
        string moduleDirectory,
        string moduleDirectoryName,
        out string entryAssemblyPath,
        out string reason)
    {
        entryAssemblyPath = string.Empty;
        reason = string.Empty;

        var preferred = Path.Combine(moduleDirectory, $"{moduleDirectoryName}.dll");
        if (File.Exists(preferred))
        {
            entryAssemblyPath = Path.GetFullPath(preferred);
            return true;
        }

        var dllFiles = Directory.EnumerateFiles(moduleDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return dllFiles.Count switch
        {
            0 => Fail("no entry DLL found", out entryAssemblyPath, out reason),
            1 => Success(dllFiles[0], out entryAssemblyPath, out reason),
            _ => Fail("multiple DLL files found; expected a single entry DLL or {name}.dll", out entryAssemblyPath, out reason),
        };

        static bool Success(string path, out string resolved, out string skipReason)
        {
            resolved = path;
            skipReason = string.Empty;
            return true;
        }

        static bool Fail(string message, out string resolved, out string skipReason)
        {
            resolved = string.Empty;
            skipReason = message;
            return false;
        }
    }
}

internal static partial class FilesystemModuleDiscovererLog
{
    [LoggerMessage(EventId = 62101, Level = LogLevel.Information, Message = "Modules root does not exist yet: {ModulesRoot}")]
    public static partial void ModulesRootMissing(ILogger logger, string modulesRoot);

    [LoggerMessage(EventId = 62102, Level = LogLevel.Warning, Message = "Skipping module directory '{ModuleDirectory}': {Reason}")]
    public static partial void ModuleDirectorySkipped(ILogger logger, string moduleDirectory, string reason);
}
