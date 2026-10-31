using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace Statevia.Architecture.Tests;

/// <summary>
/// クリーン・アーキテクチャの依存方向ルールを検証する。
/// <list type="bullet">
/// <item><c>core/*</c> → <c>infrastructure/*</c> 禁止</item>
/// <item><c>core/*</c> → <c>service/*</c> 禁止</item>
/// <item><c>infrastructure/*</c> → <c>service/*</c> 禁止</item>
/// <item><c>modules/reference/*</c> → Module が使ってよい範囲の外の Engine の型 禁止</item>
/// </list>
/// </summary>
public sealed class DependencyDirectionTests
{
    private static readonly string[] InfrastructureNamespaces =
    [
        "Statevia.Infrastructure.Persistence",
        "Statevia.Infrastructure.Security",
        "Statevia.Infrastructure.Notification",
        "Statevia.Infrastructure.Modules",
        "Statevia.Infrastructure.Actions",
        "Statevia.Infrastructure.Common",
    ];

    private static readonly string[] ServiceNamespaces =
    [
        "Statevia.Service.Api",
        "Statevia.Service.ActionHost",
        "Statevia.Service.Cli",
    ];

    private static Assembly CoreEngineAssembly =>
        typeof(Core.Engine.Abstractions.IExecutionEngine).Assembly;

    private static Assembly CoreApplicationAssembly =>
        typeof(Core.Application.DependencyInjection.ApplicationServiceCollectionExtensions).Assembly;

    private static Assembly CoreApplicationContractsAssembly =>
        typeof(Core.Application.Contracts.NotFoundException).Assembly;

    private static Assembly CoreActionsAbstractionsAssembly =>
        typeof(Core.Actions.Abstractions.Catalog.IActionCatalog).Assembly;

    /// <summary>Core.Engine は Infrastructure に依存しない。</summary>
    [Fact]
    public void CoreEngine_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(CoreEngineAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Engine → Infrastructure", result));
    }

    /// <summary>Core.Engine は Service に依存しない。</summary>
    [Fact]
    public void CoreEngine_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(CoreEngineAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Engine → Service", result));
    }

    /// <summary>Core.Application.Contracts は Infrastructure に依存しない。</summary>
    [Fact]
    public void CoreApplicationContracts_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(CoreApplicationContractsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Application.Contracts → Infrastructure", result));
    }

    /// <summary>Core.Application.Contracts は Service に依存しない。</summary>
    [Fact]
    public void CoreApplicationContracts_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(CoreApplicationContractsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Application.Contracts → Service", result));
    }

    /// <summary>Core.Application は Infrastructure に依存しない。</summary>
    [Fact]
    public void CoreApplication_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(CoreApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Application → Infrastructure", result));
    }

    /// <summary>Core.Application は Service に依存しない。</summary>
    [Fact]
    public void CoreApplication_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(CoreApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Application → Service", result));
    }

    /// <summary>Core.Actions.Abstractions は Infrastructure に依存しない。</summary>
    [Fact]
    public void CoreActionsAbstractions_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(CoreActionsAbstractionsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Actions.Abstractions → Infrastructure", result));
    }

    /// <summary>Core.Actions.Abstractions は Service に依存しない。</summary>
    [Fact]
    public void CoreActionsAbstractions_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(CoreActionsAbstractionsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Actions.Abstractions → Service", result));
    }

    private static Assembly InfraPersistenceAssembly =>
        typeof(Infrastructure.Persistence.DependencyInjection.PersistenceServiceCollectionExtensions).Assembly;

    private static Assembly InfraSecurityAssembly =>
        typeof(Infrastructure.Security.DependencyInjection.SecurityServiceCollectionExtensions).Assembly;

    private static Assembly InfraNotificationAssembly =>
        typeof(Infrastructure.Notification.DependencyInjection.NotificationServiceCollectionExtensions).Assembly;

    private static Assembly InfraModulesAssembly =>
        typeof(Infrastructure.Modules.DependencyInjection.ModulesServiceCollectionExtensions).Assembly;

    private static Assembly InfraCommonAssembly =>
        typeof(Infrastructure.Common.DefaultIdGenerator).Assembly;

    /// <summary>Infrastructure.Persistence は Service に依存しない。</summary>
    [Fact]
    public void InfraPersistence_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(InfraPersistenceAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Infrastructure.Persistence → Service", result));
    }

    /// <summary>Infrastructure.Security は Service に依存しない。</summary>
    [Fact]
    public void InfraSecurity_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(InfraSecurityAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Infrastructure.Security → Service", result));
    }

    /// <summary>Infrastructure.Notification は Service に依存しない。</summary>
    [Fact]
    public void InfraNotification_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(InfraNotificationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Infrastructure.Notification → Service", result));
    }

    /// <summary>Infrastructure.Modules は Service に依存しない。</summary>
    [Fact]
    public void InfraModules_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(InfraModulesAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Infrastructure.Modules → Service", result));
    }

    /// <summary>Infrastructure.Common は Service に依存しない。</summary>
    [Fact]
    public void InfraCommon_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(InfraCommonAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Infrastructure.Common → Service", result));
    }

    private static Assembly ReferenceHttpAssembly =>
        typeof(Reference.Http.HttpReferenceModule).Assembly;

    private static Assembly ReferenceNotificationAssembly =>
        typeof(Reference.Notification.NotificationReferenceModule).Assembly;

    /// <summary>Module が使ってよい Engine の名前空間（このほかに <c>DefaultStateExecutor</c> だけを使ってよい）。</summary>
    private const string ModuleEngineSurfaceNamespace = "Statevia.Core.Engine.Abstractions";

    /// <summary>
    /// Module が使ってよい範囲（<c>docs/specifications/actions/module-zip-layout.md</c> §3）の外にある Engine の型の完全名。
    /// </summary>
    /// <remarks>
    /// NetArchTest は依存名を名前空間・型名の単位で照合し、名前空間を渡すと下位の名前空間も含める。
    /// <c>Statevia.Core.Engine</c> を渡すと使ってよい <c>Abstractions</c> まで含むため、範囲の外の型を完全名で列挙する。
    /// 入れ子の型は外側の型と同じ範囲に属するものとし、外側の型だけを挙げる（2026-10-31 時点で、Engine に外から参照できる入れ子の型は無い）。
    /// </remarks>
    private static readonly string[] EngineTypesOutsideModuleSurface = CoreEngineAssembly
        .GetTypes()
        .Where(type => !type.IsNested
            && type.Namespace is { } typeNamespace
            && typeNamespace.StartsWith("Statevia.Core.Engine", StringComparison.Ordinal)
            && typeNamespace != ModuleEngineSurfaceNamespace
            && type != typeof(Core.Engine.Execution.DefaultStateExecutor))
        .Select(type => type.FullName!)
        .Order(StringComparer.Ordinal)
        .ToArray();

    /// <summary>範囲の外の型の一覧が、Engine の内部の型を含み、使ってよい型を含まない（ルールが空にならない）。</summary>
    [Fact]
    public void EngineTypesOutsideModuleSurface_ExcludesOnlyModuleSurface()
    {
        Assert.Contains(typeof(Core.Engine.Engine.ExecutionEngine).FullName, EngineTypesOutsideModuleSurface);
        Assert.DoesNotContain(typeof(Core.Engine.Execution.DefaultStateExecutor).FullName, EngineTypesOutsideModuleSurface);
        Assert.DoesNotContain(EngineTypesOutsideModuleSurface, name => name.StartsWith(ModuleEngineSurfaceNamespace + ".", StringComparison.Ordinal));
    }

    /// <summary>リファレンス Module（Http）は、Module が使ってよい範囲の外の Engine の型に依存しない。</summary>
    [Fact]
    public void ReferenceHttp_ShouldNotDependOn_EngineOutsideModuleSurface()
    {
        var result = Types.InAssembly(ReferenceHttpAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(EngineTypesOutsideModuleSurface)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Reference.Http → Engine（Module が使ってよい範囲の外）", result));
    }

    /// <summary>リファレンス Module（Notification）は、Module が使ってよい範囲の外の Engine の型に依存しない。</summary>
    [Fact]
    public void ReferenceNotification_ShouldNotDependOn_EngineOutsideModuleSurface()
    {
        var result = Types.InAssembly(ReferenceNotificationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(EngineTypesOutsideModuleSurface)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Reference.Notification → Engine（Module が使ってよい範囲の外）", result));
    }

    private static string FormatFailure(string rule, TestResult result)
    {
        if (result.IsSuccessful) return string.Empty;

        var violators = result.FailingTypes?.Select(t => t.FullName ?? t.Name).ToList() ?? [];
        var sample = violators.Take(10).ToList();
        var message = $"依存方向違反: {rule}\n違反型({violators.Count}件):";
        foreach (var name in sample)
            message += $"\n  - {name}";
        if (violators.Count > 10)
            message += $"\n  ...他 {violators.Count - 10} 件";
        return message;
    }
}
