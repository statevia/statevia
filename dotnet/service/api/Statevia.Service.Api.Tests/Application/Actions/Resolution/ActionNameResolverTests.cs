using Statevia.Core.Engine.Definition;
using Statevia.Service.Api.Application.Actions;
using Statevia.Service.Api.Application.Actions.Resolution;
using System.Reflection;

namespace Statevia.Service.Api.Tests.Application.Actions.Resolution;

public sealed class ActionNameResolverTests
{
    private static WorkflowDefinition CreateDefinition(
        IReadOnlyDictionary<string, string>? modules,
        params (string Name, string? Action)[] states)
    {
        var stateMap = states.ToDictionary(
            s => s.Name,
            s => new StateDefinition
            {
                Action = s.Action,
                On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Completed"] = new TransitionDefinition { End = true },
                },
            },
            StringComparer.OrdinalIgnoreCase);

        return new WorkflowDefinition
        {
            Name = "W",
            Modules = ParseModules(modules),
            States = stateMap,
        };
    }

    private static IReadOnlyDictionary<string, ModuleImportReference>? ParseModules(
        IReadOnlyDictionary<string, string>? modules) =>
        modules?.ToDictionary(
            entry => entry.Key,
            entry => ModuleImportReference.ParseImportValue(entry.Value),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>builtin 短名 noop は受理せず例外になる。</summary>
    [Fact]
    public void Resolve_BuiltinShortNameNoop_Throws()
    {
        // Arrange
        var def = CreateDefinition(null, ("A", "noop"));

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() => ActionNameResolver.Resolve(def));
        Assert.Contains("short names are not supported", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>短名 rest は受理しない。</summary>
    [Fact]
    public void Resolve_RestShortName_Throws()
    {
        // Arrange
        var def = CreateDefinition(null, ("A", "rest"));

        // Act / Assert
        Assert.Throws<ArgumentException>(() => ActionNameResolver.Resolve(def));
    }

    /// <summary>既に FQCN の action はそのまま保持される。</summary>
    [Fact]
    public void Resolve_FullyQualifiedAction_KeepsAsIs()
    {
        // Arrange
        const string fqcn = "statevia.action.reference.http.request";
        var def = CreateDefinition(null, ("A", fqcn));

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Equal(fqcn, resolved.States["A"].Action);
    }

    /// <summary>module alias と actionName は ModuleId 付き canonical ID に解決される。</summary>
    [Fact]
    public void Resolve_ModuleAliasActionName_PrefixesModuleId()
    {
        // Arrange
        var modules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mail"] = "com.company.mail",
        };
        var def = CreateDefinition(modules, ("A", "mail.send"));

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Equal("com.company.mail.send", resolved.States["A"].Action);
    }

    /// <summary>複数 module alias があっても alias.actionName で一意に解決される。</summary>
    [Fact]
    public void Resolve_MultipleModuleAliases_ResolvesByAlias()
    {
        // Arrange
        var modules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mail"] = "com.company.mail",
            ["crm"] = "com.company.crm",
        };
        var def = CreateDefinition(modules, ("A", "crm.sync"));

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Equal("com.company.crm.sync", resolved.States["A"].Action);
    }

    /// <summary>未定義 module alias は Validation Error になる。</summary>
    [Fact]
    public void Resolve_UnknownModuleAlias_Throws()
    {
        // Arrange
        var def = CreateDefinition(null, ("A", "mail.send"));

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => ActionNameResolver.Resolve(def));

        Assert.Contains("Unknown module alias 'mail'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>ドットなし短名で builtin でも module alias でもない action は unknown エラーになる。</summary>
    [Fact]
    public void Resolve_NonBuiltinShortNameWithoutDot_ThrowsUnknown()
    {
        // Arrange
        var def = CreateDefinition(null, ("A", "trade"));

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => ActionNameResolver.Resolve(def));

        Assert.Contains("Unknown action 'trade'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>action 省略状態は implicit noop canonical ID になる。</summary>
    [Fact]
    public void Resolve_OmittedAction_AppliesImplicitNoop()
    {
        // Arrange
        var def = CreateDefinition(null, ("A", null));

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Equal(WellKnownActionIds.NoOpCanonical, resolved.States["A"].Action);
    }

    /// <summary>join のみの状態は action 解決の対象外である。</summary>
    [Fact]
    public void Resolve_JoinOnlyState_LeavesActionNull()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "W",
            States = new Dictionary<string, StateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Join1"] = new StateDefinition
                {
                    Join = new JoinDefinition { All = ["A", "B"] },
                    On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Joined"] = new TransitionDefinition { End = true },
                    },
                },
            },
        };

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Null(resolved.States["Join1"].Action);
    }

    /// <summary>wait のみの状態は action 解決の対象外である。</summary>
    [Fact]
    public void Resolve_WaitOnlyState_LeavesActionNull()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "W",
            States = new Dictionary<string, StateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["W"] = new StateDefinition
                {
                    Wait = new WaitDefinition
                    {
                        Events = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["resume"] = "Next"
                        }
                    }
                }
            }
        };

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Null(resolved.States["W"].Action);
    }

    /// <summary>action を解決する状態でも、output（vars への書き込み先）を保持する。</summary>
    [Fact]
    public void Resolve_ActionStateWithOutput_KeepsOutput()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "W",
            States = new Dictionary<string, StateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["A"] = new StateDefinition
                {
                    Action = WellKnownActionIds.NoOpCanonical,
                    Output = "$.vars.order",
                    On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Completed"] = new TransitionDefinition { End = true },
                    },
                },
            },
        };

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Equal("$.vars.order", resolved.States["A"].Output);
    }

    /// <summary>
    /// action を解決する状態は、Action 以外の初期化可能なプロパティをすべて元の値のまま引き継ぐ。
    /// StateDefinition に項目を足したとき、Resolver の写し漏れをこのテストで検出する。
    /// </summary>
    [Fact]
    public void Resolve_ActionState_CopiesEveryInitPropertyExceptAction()
    {
        // Arrange
        var original = new StateDefinition
        {
            Action = WellKnownActionIds.NoOpCanonical,
            On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionDefinition { End = true },
            },
            Input = new StateInputDefinition { Path = "$.input.orderId" },
            Output = "$.vars.order",
            Retry = new RetryDefinition { Limit = 3 },
        };
        var def = new WorkflowDefinition
        {
            Name = "W",
            States = new Dictionary<string, StateDefinition>(StringComparer.OrdinalIgnoreCase) { ["A"] = original },
        };
        var copiedProperties = typeof(StateDefinition)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is not null && property.Name != nameof(StateDefinition.Action))
            .ToList();

        // Wait / Join を持つ状態は解決対象外なので、ここでは null のまま比べる。
        // それ以外の項目は、写し漏れを検出できるよう元の値を必ず埋める（新しい項目を足したらここで失敗する）。
        var propertiesRequiringValue = copiedProperties
            .Where(property => property.Name is not (nameof(StateDefinition.Wait) or nameof(StateDefinition.Join)))
            .ToList();

        // Act
        var resolved = ActionNameResolver.Resolve(def).States["A"];

        // Assert
        Assert.All(propertiesRequiringValue, property => Assert.NotNull(property.GetValue(original)));
        Assert.All(copiedProperties, property => Assert.Same(property.GetValue(original), property.GetValue(resolved)));
    }
}
