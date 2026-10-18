using Statevia.Service.Api.Application.Definition;

namespace Statevia.Service.Api.Tests.Application.Definition;

/// <summary>nodes 形式の <c>bindings</c> は定義モデルの外の参照マップに残る。</summary>
public sealed class NodesBindingLoaderTests
{
    /// <summary>action ノードの参照名がマップに残り、input には入らない。</summary>
    [Fact]
    public void Load_ActionNodeBindings_AreKeptBesideTheDefinition()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: Orders
            nodes:
              - name: Start
                type: start
                next: Query
              - name: Query
                type: action
                action: orders.query
                bindings:
                  secrets:
                    connection: order-db
                input:
                  customerId: $.vars.customerId
                next: End
              - name: End
                type: end
            """;

        // Act
        var loaded = new NodesWorkflowDefinitionLoader().LoadWithBindings(yaml);

        // Assert
        Assert.Equal("order-db", loaded.Bindings["Query"].Secrets["connection"]);
        Assert.False(loaded.Bindings.ContainsKey("Start"));
        Assert.True(loaded.Definition.States["Query"].Input!.Values!.ContainsKey("customerId"));
    }

    /// <summary>上限を 1 つ超えるスロットは Nodes 形式で拒否する。</summary>
    [Fact]
    public void Load_TooManySlots_Throws()
    {
        // Arrange
        var variables = string.Join(
            "\n",
            Enumerable.Range(1, NodeActionBindingSyntax.MaxSlotsPerState + 1)
                .Select(index => $"        s{index}: n{index}"));
        var yaml = $"""
            version: 1
            workflow:
              name: Orders
            nodes:
              - name: Start
                type: start
                next: Query
              - name: Query
                type: action
                action: orders.query
                bindings:
                  variables:
            {variables}
                next: End
              - name: End
                type: end
            """;

        // Act
        var exception = Assert.Throws<ArgumentException>(() => new NodesWorkflowDefinitionLoader().LoadWithBindings(yaml));

        // Assert
        Assert.Contains(
            $"bindings allows at most {NodeActionBindingSyntax.MaxSlotsPerState} slots.",
            exception.Message,
            StringComparison.Ordinal);
    }
}
