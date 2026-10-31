using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary>InProcess は参照があるとき復号前に失敗する。</summary>
public sealed class InProcessBindingRejectionTests
{
    /// <summary>参照付きリクエストは ActionBindingRejected になり、保護 API を必要としない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBindingsPresent_RejectsBeforeDecrypt()
    {
        // Arrange
        var sut = new InProcessBackend(new ServiceCollection().BuildServiceProvider());
        var invocation = new ActionBackendInvocation(
            new ActionExecutionRequest
            {
                ExecutionId = "exec",
                StateName = "Query",
                ActionId = "acme.order.query",
                TenantId = "00000000-0000-4000-8000-000000000001",
                BindingReferences = new ActionBindingReferences(
                    new Dictionary<string, string> { ["connection"] = "order-db" },
                    new Dictionary<string, string>()),
            },
            RuntimeInput: null);

        // Act
        var result = await sut.ExecuteAsync(invocation, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ActionBindingDispatch.RejectedCode, result.ErrorCode);
        Assert.Equal(ActionBindingDispatch.InProcessMessage, result.ErrorMessage);
        Assert.DoesNotContain("order-db", result.ErrorMessage, StringComparison.Ordinal);
    }
}
