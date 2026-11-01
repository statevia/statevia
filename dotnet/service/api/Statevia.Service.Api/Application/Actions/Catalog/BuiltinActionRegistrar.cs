using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;
using Statevia.Service.Api.Application.Actions.Builtins;
using Statevia.Service.Api.Application.Actions.Publication;

namespace Statevia.Service.Api.Application.Actions.Catalog;

/// <summary>Builtin action を Catalog へ登録する。</summary>
internal static class BuiltinActionRegistrar
{
    private const string BuiltinModuleId = "statevia.action.builtin";
    private const string BuiltinVersion = "1.0.0";

    /// <summary>全 Builtin を Catalog へ登録する。</summary>
    /// <param name="catalog">登録先 Catalog。</param>
    public static void Register(IActionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog is not InMemoryActionCatalog memoryCatalog)
        {
            throw new ArgumentException(
                "Builtin registration requires InMemoryActionCatalog.",
                nameof(catalog));
        }

        RegisterNoOp(memoryCatalog);
        RegisterSleep(memoryCatalog);
        RegisterSignal(memoryCatalog);
        RegisterPublish(memoryCatalog);
        RegisterWorkflow(memoryCatalog);
    }

    private static void RegisterNoOp(InMemoryActionCatalog catalog)
    {
        var actionId = WellKnownActionIds.NoOpCanonical;
        catalog.Register(
            CreateBuiltinDescriptor(actionId),
            new ActionCatalogEntry(InProcessFactory: CreateFactory(new NoOpState())),
            new ActionCapabilityMetadata(ActionCapabilityCategory.Transform, "No-op", IsExperimental: false),
            BuiltinActionSchemas.NoOp(actionId));
    }

    private static void RegisterSleep(InMemoryActionCatalog catalog)
    {
        var actionId = WellKnownActionIds.Sleep;
        catalog.Register(
            CreateBuiltinDescriptor(actionId),
            new ActionCatalogEntry(InProcessFactory: CreateFactory(new SleepActionState())),
            new ActionCapabilityMetadata(ActionCapabilityCategory.Timing, "Sleep", IsExperimental: false),
            BuiltinActionSchemas.Sleep(actionId));
    }

    /// <summary>
    /// 退役した signal を登録する。保存済みの版を読み込めるようにエントリだけ残し、実行するとノードを失敗させる。
    /// </summary>
    /// <remarks>
    /// Publication は保存済みの版の input 検証に使うため残す。Action 一覧と Schema API には出さない。
    /// </remarks>
    private static void RegisterSignal(InMemoryActionCatalog catalog)
    {
        var actionId = WellKnownActionIds.Signal;
        var retiredState = new RetiredActionState(actionId, RetiredBuiltinActions.SignalReplacementHint);
        catalog.Register(
            CreateBuiltinDescriptor(actionId),
            new ActionCatalogEntry(InProcessFactory: CreateFactory(retiredState), IsRetired: true),
            capabilityMetadata: null,
            BuiltinActionSchemas.Signal(actionId));
    }

    private static void RegisterPublish(InMemoryActionCatalog catalog)
    {
        var actionId = WellKnownActionIds.Publish;
        catalog.Register(
            CreateBuiltinDescriptor(actionId),
            new ActionCatalogEntry(InProcessFactory: CreateScopedFactory<PublishActionState>()),
            new ActionCapabilityMetadata(ActionCapabilityCategory.Event, "Publish", IsExperimental: false),
            BuiltinActionSchemas.Publish(actionId));
    }

    private static void RegisterWorkflow(InMemoryActionCatalog catalog)
    {
        var actionId = WellKnownActionIds.Workflow;
        catalog.Register(
            CreateBuiltinDescriptor(actionId),
            new ActionCatalogEntry(InProcessFactory: CreateScopedFactory<WorkflowActionState>()),
            new ActionCapabilityMetadata(ActionCapabilityCategory.Workflow, "Workflow", IsExperimental: true),
            BuiltinActionSchemas.Workflow(actionId));
    }

    private static ActionDescriptor CreateBuiltinDescriptor(string actionId) =>
        new()
        {
            ActionId = actionId,
            ModuleId = BuiltinModuleId,
            Version = BuiltinVersion,
            TrustLevel = ActionTrustLevel.Trusted,
            Source = ActionSourceKind.Builtin,
            OwnerTenantId = null,
            Visibility = ActionVisibility.Builtin,
            ExecutionHints = new ActionExecutionHints
            {
                PreferredMode = ActionExecutionMode.InProcess,
            },
        };

    private static Func<IServiceProvider, IStateExecutor> CreateFactory<TIn, TOut>(IState<TIn, TOut> state) =>
        _ => DefaultStateExecutor.Create(state);

    private static Func<IServiceProvider, IStateExecutor> CreateScopedFactory<TState>()
        where TState : class, IState<object?, object?> =>
        serviceProvider =>
            DefaultStateExecutor.Create(ActivatorUtilities.CreateInstance<TState>(serviceProvider));
}
