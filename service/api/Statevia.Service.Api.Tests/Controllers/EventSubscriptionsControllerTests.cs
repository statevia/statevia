using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Controllers;

namespace Statevia.Service.Api.Tests.Controllers;

/// <summary><see cref="EventSubscriptionsController"/> が候補 GET の形を返す。</summary>
public sealed class EventSubscriptionsControllerTests
{
    /// <summary>200 で topic / key だけを返し、ノード Resume 用の識別子は載せない。</summary>
    [Fact]
    public async Task List_ReturnsSubscriptionsWithoutExecutionIdentity()
    {
        // Arrange
        var query = new StubEventSubscriptionQueryService(
            [new EventSubscriptionCandidate("orders.updated", "")]);
        var controller = new EventSubscriptionsController(query);

        // Act
        var result = await controller.List(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<EventSubscriptionListResponse>(ok.Value);
        var subscription = Assert.Single(body.Subscriptions);
        Assert.Equal("orders.updated", subscription.Topic);
        Assert.Equal("", subscription.Key);
    }

    private sealed class StubEventSubscriptionQueryService(IReadOnlyList<EventSubscriptionCandidate> candidates)
        : IEventSubscriptionQueryService
    {
        public Task<IReadOnlyList<EventSubscriptionCandidate>> ListActiveAsync(CancellationToken ct) =>
            Task.FromResult(candidates);
    }
}
