using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Service.ActionHost.Modules;

namespace Statevia.Service.ActionHost.Tests;

/// <summary><see cref="FilesystemModuleDiscoverer"/> の単体テスト。</summary>
public sealed class FilesystemModuleDiscovererTests
{
    /// <summary>存在しない modules ルートは空一覧を返す。</summary>
    [Fact]
    public void Discover_WhenModulesRootMissing_ReturnsEmpty()
    {
        // Arrange
        var discoverer = new FilesystemModuleDiscoverer(NullLogger<FilesystemModuleDiscoverer>.Instance);
        var missingRoot = Path.Combine(Path.GetTempPath(), "statevia-action-host-test", Guid.NewGuid().ToString("N"));

        // Act
        var discovered = discoverer.Discover(missingRoot, CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>entry DLL が無いディレクトリはスキップされる。</summary>
    /// <summary>テナント配下で entry DLL が無い子はスキップされる。</summary>
    [Fact]
    public void Discover_WhenModuleDirectoryHasNoDll_SkipsDirectory()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(modulesRoot, "default", "empty.module"));
        var discoverer = new FilesystemModuleDiscoverer(NullLogger<FilesystemModuleDiscoverer>.Instance);

        // Act
        var discovered = discoverer.Discover(modulesRoot, CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>複数 DLL があるディレクトリはスキップされる。</summary>
    /// <summary>テナント配下で複数 DLL がある子はスキップされる。</summary>
    [Fact]
    public void Discover_WhenMultipleDllsExist_SkipsDirectory()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var moduleDirectory = Path.Combine(modulesRoot, "default", "multi.module");
        Directory.CreateDirectory(moduleDirectory);
        File.WriteAllText(Path.Combine(moduleDirectory, "a.dll"), "a");
        File.WriteAllText(Path.Combine(moduleDirectory, "b.dll"), "b");
        var discoverer = new FilesystemModuleDiscoverer(NullLogger<FilesystemModuleDiscoverer>.Instance);

        // Act
        var discovered = discoverer.Discover(modulesRoot, CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>テナント配下の入口 DLL を発見し、テナントディレクトリと直下の平らな配置は Module にしない。</summary>
    [Fact]
    public void Discover_WhenTenantChildHasEntryDll_ReturnsThatModuleOnly()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var tenantDirectory = Path.Combine(modulesRoot, "default");
        Directory.CreateDirectory(tenantDirectory);
        File.WriteAllText(Path.Combine(tenantDirectory, "default.dll"), "tenant");
        var flatModuleDirectory = Path.Combine(modulesRoot, "flat.module");
        Directory.CreateDirectory(flatModuleDirectory);
        File.WriteAllText(Path.Combine(flatModuleDirectory, "flat.module.dll"), "flat");
        var moduleDirectory = Path.Combine(tenantDirectory, "statevia.action.reference.http");
        Directory.CreateDirectory(moduleDirectory);
        var dllPath = Path.Combine(moduleDirectory, "statevia.action.reference.http.dll");
        File.WriteAllText(dllPath, "dll");
        var discoverer = new FilesystemModuleDiscoverer(NullLogger<FilesystemModuleDiscoverer>.Instance);

        // Act
        var discovered = discoverer.Discover(modulesRoot, CancellationToken.None);

        // Assert
        var module = Assert.Single(discovered);
        Assert.Equal("statevia.action.reference.http", module.ModuleDirectoryName);
        Assert.Equal(Path.GetFullPath(dllPath), module.EntryAssemblyPath);
    }

    /// <summary>Module ディレクトリより下の bin にある DLL は発見しない。</summary>
    [Fact]
    public void Discover_WhenEntryDllIsUnderBin_SkipsModule()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var binDirectory = Path.Combine(modulesRoot, "default", "some.module", "bin");
        Directory.CreateDirectory(binDirectory);
        File.WriteAllText(Path.Combine(binDirectory, "some.module.dll"), "dll");
        var discoverer = new FilesystemModuleDiscoverer(NullLogger<FilesystemModuleDiscoverer>.Instance);

        // Act
        var discovered = discoverer.Discover(modulesRoot, CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>複数テナントの Module を同じ一覧へ載せる。</summary>
    [Fact]
    public void Discover_WhenMultipleTenantsHaveModules_ReturnsEachModule()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        WriteEntryDll(modulesRoot, "default", "alpha.module");
        WriteEntryDll(modulesRoot, "other", "beta.module");
        var discoverer = new FilesystemModuleDiscoverer(NullLogger<FilesystemModuleDiscoverer>.Instance);

        // Act
        var discovered = discoverer.Discover(modulesRoot, CancellationToken.None);

        // Assert
        Assert.Equal(2, discovered.Count);
        Assert.Contains(discovered, module => module.ModuleDirectoryName == "alpha.module");
        Assert.Contains(discovered, module => module.ModuleDirectoryName == "beta.module");
    }

    /// <summary>modules ルートの外にある入口パスは拒否する。</summary>
    [Fact]
    public void IsEntryInsideModulesRoot_WhenPathIsOutside_ReturnsFalse()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var outsideDll = Path.Combine(CreateTempDirectory(), "outside.dll");
        File.WriteAllText(outsideDll, "dll");

        // Act
        var inside = FilesystemModuleDiscoverer.IsEntryInsideModulesRoot(
            Path.GetFullPath(modulesRoot),
            Path.GetFullPath(outsideDll));

        // Assert
        Assert.False(inside);
    }

    private static void WriteEntryDll(string modulesRoot, string tenantKey, string moduleDirectoryName)
    {
        var moduleDirectory = Path.Combine(modulesRoot, tenantKey, moduleDirectoryName);
        Directory.CreateDirectory(moduleDirectory);
        File.WriteAllText(Path.Combine(moduleDirectory, $"{moduleDirectoryName}.dll"), "dll");
    }

    /// <summary>単一 DLL のみのディレクトリは発見される。</summary>
    [Fact]
    public void TryResolveEntryAssemblyPath_WhenSingleDll_ReturnsPath()
    {
        // Arrange
        var moduleDirectory = CreateTempDirectory();
        var dllPath = Path.Combine(moduleDirectory, "only.dll");
        File.WriteAllText(dllPath, "dll");

        // Act
        var resolved = FilesystemModuleDiscoverer.TryResolveEntryAssemblyPath(
            moduleDirectory,
            "ignored",
            out var entryAssemblyPath,
            out var reason);

        // Assert
        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(dllPath), entryAssemblyPath);
        Assert.Empty(reason);
    }

    /// <summary>entry DLL が無い場合は失敗理由を返す。</summary>
    [Fact]
    public void TryResolveEntryAssemblyPath_WhenNoDll_ReturnsFailureReason()
    {
        // Arrange
        var moduleDirectory = CreateTempDirectory();

        // Act
        var resolved = FilesystemModuleDiscoverer.TryResolveEntryAssemblyPath(
            moduleDirectory,
            "missing.module",
            out var entryAssemblyPath,
            out var reason);

        // Assert
        Assert.False(resolved);
        Assert.Empty(entryAssemblyPath);
        Assert.Equal("no entry DLL found", reason);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "statevia-action-host-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
