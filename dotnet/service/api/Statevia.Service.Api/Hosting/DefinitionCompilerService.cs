using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Visibility;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Statevia.Service.Api.Application.Actions.Builtins;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Service.Api.Application.Actions.Resolution;
using Statevia.Service.Api.Application.Actions.Validation;
using Statevia.Service.Api.Application.Actions.Versioning;
using Statevia.Service.Api.Application.Definition;
using System.Diagnostics.CodeAnalysis;

namespace Statevia.Service.Api.Hosting;

/// <summary>
/// YAML を検証・コンパイルして CompiledWorkflowDefinition を返す。Action Catalog で action を検証・解決する。
/// <see cref="ValidateAndCompile"/> の JSON には <c>transitions</c> に加え <c>conditionalTransitions</c>・<c>stateInputs</c> を含め、条件遷移のデバッグ・UI 表示に利用できる。
/// </summary>
internal sealed class DefinitionCompilerService : IDefinitionCompilerService
{
    private static readonly System.Text.Json.JsonSerializerOptions s_compiledJsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    private readonly DefinitionLoadStrategy _definitionLoadStrategy;
    private readonly IActionCatalog _actionCatalog;
    private readonly IActionVisibilityResolver _visibilityResolver;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DefinitionCompilerService> _logger;

    /// <summary>
    /// 定義コンパイラサービスを構築する。
    /// </summary>
    /// <param name="actionCatalog">Action Catalog。</param>
    /// <param name="visibilityResolver">Visibility 判定。</param>
    /// <param name="definitionLoadStrategy">YAML ロード戦略。</param>
    /// <param name="serviceProvider">状態実行器ファクトリ生成用。</param>
    /// <param name="logger">Schema 未提供 action の warning ログ用。</param>
    public DefinitionCompilerService(
        IActionCatalog actionCatalog,
        IActionVisibilityResolver visibilityResolver,
        DefinitionLoadStrategy definitionLoadStrategy,
        IServiceProvider serviceProvider,
        ILogger<DefinitionCompilerService> logger)
    {
        _actionCatalog = actionCatalog;
        _visibilityResolver = visibilityResolver;
        _definitionLoadStrategy = definitionLoadStrategy ?? throw new ArgumentNullException(nameof(definitionLoadStrategy));
        _serviceProvider = serviceProvider;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public (CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(
        string name,
        string yaml,
        Guid? tenantId = null)
    {
        var loaded = _definitionLoadStrategy.LoadWithBindings(yaml);
        var def = loaded.Definition;
        var compileBindings = ModuleActionCompileBinder.Bind(def, _actionCatalog);
        def = ResolveActionNames(def);
        var validation = DefinitionValidator.Validate(def);
        if (!validation.IsValid)
        {
            throw new DefinitionValidationException(validation);
        }

        ValidateRegisteredActions(compileBindings.StateActionBindings, tenantId, allowRetired: false);
        ValidateActionInputs(def, compileBindings.StateActionBindings);

        var factory = new ActionExecutorFactory(
            def,
            compileBindings.StateActionBindings,
            _actionCatalog,
            _serviceProvider,
            loaded.NodeBindings);
        var compiler = new DefinitionCompiler(factory);
        var compiled = compiler.Compile(
            def,
            compileBindings.ResolvedModules,
            compileBindings.StateActionBindings);
        var compiledJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            compiled.Name,
            initialState = compiled.InitialState,
            transitions = compiled.Transitions,
            conditionalTransitions = compiled.ConditionalTransitions,
            forkTable = compiled.ForkTable,
            joinTable = compiled.JoinTable,
            waitEventRouteTable = compiled.WaitEventRouteTable,
            waitSubscriptions = compiled.WaitSubscriptions,
            stateInputs = compiled.StateInputs,
            stateOutputs = compiled.StateOutputs,
            resolvedModules = compiled.ResolvedModules,
            stateActionBindings = compiled.StateActionBindings,
        }, s_compiledJsonOptions);
        return (compiled, compiledJson);
    }

    /// <inheritdoc />
    public CompiledWorkflowDefinition RestoreFromStoredVersion(string sourceYaml, string compiledJson)
    {
        var loaded = _definitionLoadStrategy.LoadWithBindings(sourceYaml);
        var def = loaded.Definition;
        var compileBindings = StoredDefinitionBindingResolver.Resolve(def, compiledJson, _actionCatalog);
        def = ResolveActionNames(def);
        ValidateRegisteredActions(compileBindings.StateActionBindings, tenantId: null, allowRetired: true);
        ValidateActionInputs(def, compileBindings.StateActionBindings);
        var factory = new ActionExecutorFactory(
            def,
            compileBindings.StateActionBindings,
            _actionCatalog,
            _serviceProvider,
            loaded.NodeBindings);
        return CompiledDefinitionJsonReader.Read(
            compiledJson,
            factory,
            compileBindings.ResolvedModules,
            compileBindings.StateActionBindings,
            waitSubscriptions: DefinitionCompiler.CreateWaitSubscriptions(def));
    }

    private static WorkflowDefinition ResolveActionNames(WorkflowDefinition def) =>
        ActionNameResolver.Resolve(def);

    /// <summary>
    /// 状態が参照する Action が Catalog に登録済みで、テナントから使えることを確かめる。
    /// </summary>
    /// <param name="bindings">状態名 → Action の束縛。</param>
    /// <param name="tenantId">可視性を確かめるテナント。null なら確かめない（保存済みの版の読み込み）。</param>
    /// <param name="allowRetired">
    /// 退役した Action を許すか。新しい定義の保存・更新・検証では false、保存済みの版の読み込みでは true。
    /// </param>
    /// <exception cref="ArgumentException">未登録、退役（許さないとき）、または不可視の Action を参照している。</exception>
    private void ValidateRegisteredActions(
        IReadOnlyDictionary<string, Statevia.Core.Engine.Definition.StateActionBinding> bindings,
        Guid? tenantId,
        bool allowRetired)
    {
        foreach (var (stateName, binding) in bindings)
        {
            if (!TryGetVersionedRegistration(binding, out var registration)
                && !_actionCatalog.TryGetRegistration(binding.LogicalActionId, out registration))
            {
                throw new ArgumentException(
                    $"Unknown action '{binding.LogicalActionId}' in state '{stateName}'.");
            }

            if (!allowRetired && registration.Entry.IsRetired)
            {
                throw new ArgumentException(
                    RetiredBuiltinActions.FormatRejection(binding.LogicalActionId, stateName));
            }

            var descriptor = registration.Descriptor;
            if (tenantId is not null
                && !_visibilityResolver.CanUse(tenantId.Value.ToString("D"), descriptor))
            {
                throw new ArgumentException(
                    $"Action '{binding.LogicalActionId}' is not visible to the current tenant in state '{stateName}'.");
            }
        }
    }

    private bool TryGetVersionedRegistration(
        Statevia.Core.Engine.Definition.StateActionBinding binding,
        [NotNullWhen(true)] out ActionRegistration? registration)
    {
        registration = null;
        if (binding.ResolvedModuleVersion is not { Length: > 0 } version
            || binding.ModuleId is not { Length: > 0 } moduleId
            || binding.ActionName is not { Length: > 0 } actionName)
        {
            return false;
        }

        return _actionCatalog.TryGetRegistration(moduleId, version, actionName, out registration);
    }

    private void ValidateActionInputs(
        WorkflowDefinition def,
        IReadOnlyDictionary<string, Statevia.Core.Engine.Definition.StateActionBinding> bindings)
    {
        var errors = new List<ActionInputValidationError>();
        foreach (var (stateName, binding) in bindings)
        {
            if (!def.States.TryGetValue(stateName, out var state))
            {
                continue;
            }

            var actionId = binding.LogicalActionId;
            if (!TryGetPublication(binding, out var publication) || publication is null)
            {
                HandleMissingPublication(stateName, actionId, errors);
                continue;
            }

            errors.AddRange(ActionInputSchemaValidator.Validate(
                stateName,
                actionId,
                state.Input,
                publication.SchemaBundle.InputSchema.RootElement));
        }

        if (errors.Count > 0)
        {
            throw new ActionInputSchemaValidationException(errors);
        }
    }

    private bool TryGetPublication(
        Statevia.Core.Engine.Definition.StateActionBinding binding,
        out Statevia.Core.Actions.Abstractions.Publication.ActionPublication? publication)
    {
        publication = null;
        if (binding.ResolvedModuleVersion is { Length: > 0 } version
            && binding.ModuleId is { Length: > 0 } moduleId
            && binding.ActionName is { Length: > 0 } actionName)
        {
            return _actionCatalog.TryGetPublication(moduleId, version, actionName, out publication);
        }

        return _actionCatalog.TryGetPublication(binding.LogicalActionId, out publication);
    }

    private void HandleMissingPublication(
        string stateName,
        string actionId,
        List<ActionInputValidationError> errors)
    {
        if (ShouldRequirePublication(actionId))
        {
            errors.Add(new ActionInputValidationError(
                stateName,
                actionId,
                "$.input",
                $"Action '{actionId}' has no registered input schema."));
            return;
        }

        DefinitionCompilerLogMessages.ActionInputSchemaMissing(_logger, stateName, actionId);
    }

    private bool ShouldRequirePublication(string actionId)
    {
        if (ActionInputSchemaValidationOptions.RequireSchemaForUnpublishedActions)
        {
            return true;
        }

        if (!ActionInputSchemaValidationOptions.RequireSchemaForModuleActions)
        {
            return false;
        }

        if (!_actionCatalog.TryGetDescriptor(actionId, out var descriptor))
        {
            return false;
        }

        return descriptor.Visibility != ActionVisibility.Builtin;
    }

    /// <summary>起動時に組み込みアクションを Catalog へ登録する。</summary>
    /// <param name="catalog">登録先 Catalog。</param>
    public static void RegisterBuiltinActions(IActionCatalog catalog) =>
        BuiltinActionRegistrar.Register(catalog);
}
