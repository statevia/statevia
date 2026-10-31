using Statevia.Core.Engine.Definition;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary>States 形式は <c>bindings</c> をエンジンの定義として読まない。</summary>
public sealed class StateBindingLoaderTests
{
    /// <summary>参照キーがあっても States 形式のロードは成功し、action と input だけが残る。</summary>
    [Fact]
    public void Load_IgnoresBindingsKey()
    {
        // Arrange
        var yaml = """
            workflow:
              name: Orders
            states:
              Query:
                action: orders.query
                bindings:
                  secrets:
                    connection: order-db
                input:
                  customerId: $.vars.customerId
                on:
                  Completed:
                    end: true
            """;

        // Act
        var definition = new StateWorkflowDefinitionLoader().Load(yaml);
        var query = definition.States["Query"];

        // Assert
        Assert.Equal("orders.query", query.Action);
        Assert.True(query.Input!.Values!.ContainsKey("customerId"));
    }
}
