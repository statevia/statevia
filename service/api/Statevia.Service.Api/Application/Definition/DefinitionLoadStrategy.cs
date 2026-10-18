using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Definition;

namespace Statevia.Service.Api.Application.Definition;

/// <summary>
/// ルート形式に応じて <see cref="StateWorkflowDefinitionLoader"/>（states）または <see cref="NodesWorkflowDefinitionLoader"/>（nodes）へ委譲する既定ストラテジ。
/// </summary>
internal sealed class DefinitionLoadStrategy : IDefinitionLoadStrategy
{
    private readonly StateWorkflowDefinitionLoader _statesLoader;
    private readonly NodesWorkflowDefinitionLoader _nodesLoader;

    public DefinitionLoadStrategy(StateWorkflowDefinitionLoader statesLoader, NodesWorkflowDefinitionLoader nodesLoader)
    {
        _statesLoader = statesLoader ?? throw new ArgumentNullException(nameof(statesLoader));
        _nodesLoader = nodesLoader ?? throw new ArgumentNullException(nameof(nodesLoader));
    }

    private static readonly IReadOnlyDictionary<string, ActionBindingReferences> NoNodeBindings =
        new Dictionary<string, ActionBindingReferences>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public WorkflowDefinition Load(string content) => LoadWithBindings(content).Definition;

    /// <summary>定義を読む。Nodes 形式のときだけ action 参照マップを添える。</summary>
    /// <param name="content">定義 YAML。</param>
    /// <returns>エンジン定義と、ノード名から参照へのマップ。States 形式では空。</returns>
    internal (WorkflowDefinition Definition, IReadOnlyDictionary<string, ActionBindingReferences> NodeBindings) LoadWithBindings(
        string content)
    {
        var kind = WorkflowDefinitionYamlFormat.Analyze(content);
        return kind switch
        {
            WorkflowDefinitionYamlFormatKind.Nodes => _nodesLoader.LoadWithBindings(content),
            WorkflowDefinitionYamlFormatKind.States => (_statesLoader.Load(content), NoNodeBindings),
            _ => throw new InvalidOperationException($"Unknown workflow definition format '{kind}'."),
        };
    }
}

