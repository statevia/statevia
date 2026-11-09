namespace Statevia.Infrastructure.Modules;

/// <summary>ローカル filesystem から Action Module を発見する（load は行わない）。</summary>
/// <remarks>
/// <para>正本レイアウト: <c>{modulesRoot}/{tenantKey}/{moduleName}/</c>。</para>
/// <para>
/// <see cref="ModuleDiscoveryContext.TenantKey"/> は必須。未設定時は空を返し discover しない。
/// </para>
/// </remarks>
internal sealed class FilesystemModuleSource : IModuleSource
{
    /// <summary>
    /// Filesystem Source の既定優先度。リモート Source（OCI 等）より高くも低くもない
    /// 中位値を採り、単独利用時の後方互換（従来挙動）を保つための基準点とする。
    /// </summary>
    internal const int DefaultPriority = 100;

    private readonly IResolvedModulePathProvider _pathProvider;
    private readonly ILogger<FilesystemModuleSource> _logger;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="pathProvider">解決済み modules ルート。</param>
    /// <param name="logger">ログ。</param>
    public FilesystemModuleSource(
        IResolvedModulePathProvider pathProvider,
        ILogger<FilesystemModuleSource> logger)
    {
        _pathProvider = pathProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public int Priority => DefaultPriority;

    /// <inheritdoc />
    public Task<IReadOnlyList<DiscoveredModule>> DiscoverAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantKey = ModuleDiscoveryContext.TenantKey;
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            FilesystemModuleSourceLog.TenantKeyRequired(_logger);
            return Task.FromResult<IReadOnlyList<DiscoveredModule>>(Array.Empty<DiscoveredModule>());
        }

        var modulesRoot = _pathProvider.ModulesRoot;
        if (!Directory.Exists(modulesRoot))
        {
            FilesystemModuleSourceLog.ModulesRootMissing(_logger, modulesRoot);
            return Task.FromResult<IReadOnlyList<DiscoveredModule>>(Array.Empty<DiscoveredModule>());
        }

        var discovered = new List<DiscoveredModule>();
        DiscoverUnderTenantDirectory(
            discovered,
            Path.Combine(modulesRoot, tenantKey),
            cancellationToken);

        return Task.FromResult<IReadOnlyList<DiscoveredModule>>(discovered);
    }

    private void DiscoverUnderTenantDirectory(
        List<DiscoveredModule> discovered,
        string tenantDirectory,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(tenantDirectory))
        {
            return;
        }

        foreach (var moduleDirectory in Directory.EnumerateDirectories(tenantDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryAddModule(discovered, moduleDirectory);
        }
    }

    private void TryAddModule(List<DiscoveredModule> discovered, string moduleDirectory)
    {
        var moduleDirectoryName = Path.GetFileName(moduleDirectory);
        if (string.IsNullOrWhiteSpace(moduleDirectoryName))
        {
            return;
        }

        if (!TryResolveEntryAssemblyPath(moduleDirectory, moduleDirectoryName, out var entryAssemblyPath, out var reason))
        {
            FilesystemModuleSourceLog.ModuleDirectorySkipped(_logger, moduleDirectoryName, reason);
            return;
        }

        discovered.Add(new DiscoveredModule(
            moduleDirectoryName,
            entryAssemblyPath,
            SourceLabel: "filesystem"));
    }

    /// <summary>Module ディレクトリ直下からエントリ DLL の絶対パスを決める。</summary>
    /// <remarks>
    /// <c>{ディレクトリ名}.dll</c> があればそれを選ぶ。無ければ直下の DLL がちょうど 1 つのときだけそれを選ぶ。
    /// 0 個または 2 個以上なら決められないので失敗にし、理由を返す。サブディレクトリは見ない。
    /// </remarks>
    /// <param name="moduleDirectory">Module ディレクトリの絶対パス。</param>
    /// <param name="moduleDirectoryName">Module ディレクトリ名。優先するエントリ DLL の名前に使う。</param>
    /// <param name="entryAssemblyPath">決まったエントリ DLL の絶対パス。失敗時は空文字。</param>
    /// <param name="reason">失敗の理由（ログ用の英語の文）。成功時は空文字。</param>
    /// <returns>エントリ DLL を決められたら <see langword="true"/>。</returns>
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

/// <summary><see cref="FilesystemModuleSource"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。</summary>
internal static partial class FilesystemModuleSourceLog
{
    [LoggerMessage(EventId = 60101, Level = LogLevel.Information, Message = "Modules root does not exist yet: {ModulesRoot}")]
    public static partial void ModulesRootMissing(ILogger logger, string modulesRoot);

    [LoggerMessage(EventId = 60102, Level = LogLevel.Warning, Message = "Skipping module directory '{ModuleDirectory}': {Reason}")]
    public static partial void ModuleDirectorySkipped(ILogger logger, string moduleDirectory, string reason);

    [LoggerMessage(
        EventId = 60103,
        Level = LogLevel.Warning,
        Message = "Filesystem module discover skipped: ModuleDiscoveryContext.TenantKey is required")]
    public static partial void TenantKeyRequired(ILogger logger);
}
