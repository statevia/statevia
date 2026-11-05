using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition.Validation;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// YAML/JSON の WorkflowDefinition をコンパイルし、実行可能な CompiledWorkflowDefinition を生成します。
/// 遷移テーブル、Fork/Join テーブル、Wait テーブルなどを構築します。
/// </summary>
/// <remarks>
/// 既定では、Join 以外のすべての状態に実行器が登録されているかをコンパイル時に確かめる。
/// 実行器の無い状態は、実行時にそこへ遷移した時点で実行が Failed になるため、定義と登録の食い違いを実行前に知らせる。
/// </remarks>
/// <param name="executorFactory">コンパイル済み定義に埋め込む <see cref="IStateExecutorFactory"/>。</param>
/// <param name="requireStateExecutors">
/// true（既定）のとき、Join 以外の状態で <see cref="IStateExecutorFactory.GetExecutor"/> が null を返すと
/// <see cref="DefinitionValidationException"/> を投げる。コンパイルより前に同じことを別の手段で確かめている呼び出し元
/// （Service API など）や、遷移表の構築だけを見るテストでは false を渡す。
/// </param>
public sealed class DefinitionCompiler(IStateExecutorFactory executorFactory, bool requireStateExecutors = true)
{
    /// <summary>実行器の無い状態を指す <see cref="ValidationIssue.RuleId"/>。</summary>
    public const string StateExecutorMissingRuleId = "Compile.StateExecutorMissing";

    /// <summary>ワークフロー定義をコンパイルし、実行可能なコンパイル済み定義を返します。</summary>
    /// <param name="definition">ワークフロー定義。</param>
    /// <param name="resolvedModules">compile 時に確定した module alias → 版ピン。</param>
    /// <param name="stateActionBindings">状態別 Action バインディング。</param>
    /// <exception cref="DefinitionValidationException">
    /// <c>requireStateExecutors</c> が true で、実行器の無い状態があるとき。メッセージと <see cref="DefinitionValidationException.Issues"/> に該当する状態をすべて入れる。
    /// </exception>
    public CompiledWorkflowDefinition Compile(
        WorkflowDefinition definition,
        IReadOnlyDictionary<string, ResolvedModuleBinding>? resolvedModules = null,
        IReadOnlyDictionary<string, StateActionBinding>? stateActionBindings = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var joinTable = BuildJoinTable(definition);
        if (requireStateExecutors)
        {
            EnsureStateExecutors(definition, joinTable);
        }

        return new CompiledWorkflowDefinition
        {
            Name = definition.Name,
            Transitions = BuildTransitionTable(definition),
            ConditionalTransitions = BuildConditionalTransitionTable(definition),
            ForkTable = BuildForkTable(definition),
            JoinTable = joinTable,
            WaitEventRouteTable = BuildWaitEventRouteTable(definition),
            WaitSubscriptions = BuildWaitSubscriptions(definition),
            StateInputs = BuildStateInputTable(definition),
            StateOutputs = BuildStateOutputTable(definition),
            InitialState = DetermineInitialState(definition),
            StateExecutorFactory = executorFactory,
            ResolvedModules = resolvedModules ?? EmptyResolvedModules,
            StateActionBindings = stateActionBindings ?? EmptyStateActionBindings,
        };
    }

    /// <summary>
    /// Join 以外の状態で実行器が取れないものを集め、1 つでもあれば例外にする。
    /// Join の状態は Engine が実行器を使わずに処理するため対象外とする（<c>ExecutionEngine.ScheduleStateAsync</c> と同じ判定）。
    /// </summary>
    private void EnsureStateExecutors(
        WorkflowDefinition definition,
        Dictionary<string, IReadOnlyList<string>> joinTable)
    {
        var issues = definition.States.Keys
            .Where(stateName => !joinTable.ContainsKey(stateName) && executorFactory.GetExecutor(stateName) is null)
            .Select(stateName => new ValidationIssue(
                StateExecutorMissingRuleId,
                $"State '{stateName}' has no registered executor.",
                stateName))
            .ToArray();
        if (issues.Length > 0)
        {
            throw new DefinitionValidationException(new ValidationResult(issues));
        }
    }

    private static readonly IReadOnlyDictionary<string, ResolvedModuleBinding> EmptyResolvedModules =
        new Dictionary<string, ResolvedModuleBinding>(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, StateActionBinding> EmptyStateActionBindings =
        new Dictionary<string, StateActionBinding>(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, StateInputDefinition> BuildStateInputTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, StateInputDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.Input != null)
            {
                result[stateName] = stateDef.Input;
            }
        }
        return result;
    }

    private static Dictionary<string, string> BuildStateOutputTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (!string.IsNullOrWhiteSpace(stateDef.Output))
            {
                result[stateName] = stateDef.Output;
            }
        }

        return result;
    }

    private static Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>> BuildTransitionTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, Dictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.On == null)
            {
                continue;
            }
            var stateTransitions = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase);
            foreach (var (fact, trans) in stateDef.On)
            {
                if (stateName.Equals(trans.Next, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var target = ToTransitionTarget(trans);
                if (target.Next != null || target.Fork != null || target.End)
                {
                    stateTransitions[fact] = target;
                }
            }
            if (stateTransitions.Count > 0)
            {
                result[stateName] = stateTransitions;
            }
        }
        return result.ToDictionary(k => k.Key, v => (IReadOnlyDictionary<string, TransitionTarget>)v.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, IReadOnlyDictionary<string, CompiledFactTransition>> BuildConditionalTransitionTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, Dictionary<string, CompiledFactTransition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.On is null)
            {
                continue;
            }

            var stateTransitions = new Dictionary<string, CompiledFactTransition>(StringComparer.OrdinalIgnoreCase);
            foreach (var (fact, transition) in stateDef.On)
            {
                stateTransitions[fact] = CompileFactTransition(transition);
            }

            if (stateTransitions.Count > 0)
            {
                result[stateName] = stateTransitions;
            }
        }

        return result.ToDictionary(
            keySelector: pair => pair.Key,
            elementSelector: pair => (IReadOnlyDictionary<string, CompiledFactTransition>)pair.Value,
            comparer: StringComparer.OrdinalIgnoreCase);
    }

    private static CompiledFactTransition CompileFactTransition(TransitionDefinition transition)
    {
        var linearTarget = ToTransitionTarget(transition);
        var defaultTarget = transition.Default is null ? null : ToTransitionTarget(transition.Default);
        var orderedCases = CompileCases(transition.Cases);

        return new CompiledFactTransition
        {
            LinearTarget = linearTarget is { Next: null, Fork: null, End: false } ? null : linearTarget,
            Cases = orderedCases,
            DefaultTarget = defaultTarget is { Next: null, Fork: null, End: false } ? null : defaultTarget
        };
    }

    private static List<CompiledTransitionCase> CompileCases(IReadOnlyList<TransitionCaseDefinition>? cases)
    {
        if (cases is null || cases.Count == 0)
        {
            return [];
        }

        return cases
            .Select((transitionCase, index) => new CompiledTransitionCase
            {
                Order = transitionCase.Order,
                DeclarationIndex = index,
                When = transitionCase.When,
                Target = ToTransitionTarget(transitionCase.Transition)
            })
            .OrderBy(transitionCase => transitionCase.Order.HasValue ? 0 : 1)
            .ThenBy(transitionCase => transitionCase.Order ?? int.MaxValue)
            .ThenBy(transitionCase => transitionCase.DeclarationIndex)
            .ToList();
    }

    private static TransitionTarget ToTransitionTarget(TransitionDefinition transition) =>
        new() { Next = transition.Next, Fork = transition.Fork, End = transition.End };

    private static Dictionary<string, IReadOnlyList<string>> BuildForkTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.On == null)
            {
                continue;
            }
            foreach (var (_, trans) in stateDef.On)
            {
                if (trans.Fork != null && trans.Fork.Count > 0)
                {
                    result[stateName] = trans.Fork;
                    break;
                }
            }
        }
        return result;
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildJoinTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.Join != null && stateDef.Join.All.Count > 0)
            {
                result[stateName] = stateDef.Join.All;
            }
        }
        return result;
    }

    /// <summary>
    /// Wait.events / Wait.subscribe から状態名 →（イベント名 → ルート）の表を構築する。
    /// </summary>
    private static Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>> BuildWaitEventRouteTable(
        WorkflowDefinition definition)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.Wait == null)
                continue;

            var routes = new Dictionary<string, WaitEventRouteDefinition>(StringComparer.OrdinalIgnoreCase);
            if (stateDef.Wait.Subscribe.Count > 0)
            {
                for (var i = 0; i < stateDef.Wait.Subscribe.Count; i++)
                {
                    var entry = stateDef.Wait.Subscribe[i];
                    routes[WaitSubscribeEventNames.ForIndex(i)] = new WaitEventRouteDefinition
                    {
                        Next = (entry.Next ?? string.Empty).Trim()
                    };
                }
            }
            else if (stateDef.Wait.Events.Count > 0)
            {
                foreach (var (eventName, next) in stateDef.Wait.Events)
                {
                    var trimmedEvent = eventName.Trim();
                    routes[trimmedEvent] = new WaitEventRouteDefinition
                    {
                        Next = (next ?? string.Empty).Trim()
                    };
                }
            }
            else
            {
                continue;
            }

            result[stateName] = routes;
        }

        return result;
    }

    /// <summary>
    /// Wait.subscribe から明示購読表を構築する。
    /// </summary>
    /// <remarks>
    /// compiled_json に <c>waitSubscriptions</c> が無い定義版を、同一版の YAML から復元するときに使う。
    /// </remarks>
    /// <param name="definition">ワークフロー定義。</param>
    /// <returns>状態名 → 購読エントリ。Subscribe が無い状態は含まない。</returns>
    public static IReadOnlyDictionary<string, IReadOnlyList<WaitSubscriptionDefinition>> CreateWaitSubscriptions(
        WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return BuildWaitSubscriptions(definition);
    }

    /// <summary>Wait.subscribe 明示購読を状態名キーで構築する。</summary>
    private static Dictionary<string, IReadOnlyList<WaitSubscriptionDefinition>> BuildWaitSubscriptions(
        WorkflowDefinition definition)
    {
        var result = new Dictionary<string, IReadOnlyList<WaitSubscriptionDefinition>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.Wait == null || stateDef.Wait.Subscribe.Count == 0)
                continue;

            var entries = stateDef.Wait.Subscribe
                .Select((entry, index) => new WaitSubscriptionDefinition
                {
                    Topic = entry.Topic.Trim(),
                    Key = NormalizeKey(entry.Key),
                    ResumeEventName = WaitSubscribeEventNames.ForIndex(index),
                    Next = entry.Next.Trim()
                })
                .ToList();

            result[stateName] = entries;
        }

        return result;
    }

    /// <summary>key 未指定・空白を空文字へ正規化する。</summary>
    private static string NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string DetermineInitialState(WorkflowDefinition definition)
    {
        var allReferenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, stateDef) in definition.States)
        {
            if (stateDef.On != null)
            {
                foreach (var (_, trans) in stateDef.On)
                {
                    CollectTransitionReferences(trans, allReferenced);
                }
            }
            if (stateDef.Join?.All != null)
            {
                foreach (var s in stateDef.Join.All)
                {
                    allReferenced.Add(s);
                }
            }
        }
        return definition.States.Keys.FirstOrDefault(s => !allReferenced.Contains(s))
            ?? definition.States.Keys.First()
            ?? throw new InvalidOperationException("No states in definition");
    }

    private static void CollectTransitionReferences(TransitionDefinition? transition, HashSet<string> allReferenced)
    {
        if (transition is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(transition.Next))
        {
            allReferenced.Add(transition.Next);
        }

        if (transition.Fork is { Count: > 0 })
        {
            foreach (var forkTarget in transition.Fork)
            {
                allReferenced.Add(forkTarget);
            }
        }

        if (transition.Cases is not null)
        {
            foreach (var transitionCase in transition.Cases)
            {
                CollectTransitionReferences(transitionCase.Transition, allReferenced);
            }
        }

        CollectTransitionReferences(transition.Default, allReferenced);
    }
}
