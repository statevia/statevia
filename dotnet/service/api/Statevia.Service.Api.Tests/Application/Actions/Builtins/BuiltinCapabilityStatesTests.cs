using Statevia.Core.Engine.Abstractions;
using Statevia.Service.Api.Application.Actions.Builtins;

namespace Statevia.Service.Api.Tests.Application.Actions.Builtins;

/// <summary>sleep / signal Builtin の単体テスト。</summary>
public sealed class BuiltinCapabilityStatesTests
{
    private sealed class FakeEventProvider : IEventProvider
    {
        public Task<string> WaitForEventAsync(string nodeId, IReadOnlyList<string> eventNames, CancellationToken ct) =>
            Task.FromResult(eventNames[0]);

        public void Resume(string nodeId, string eventName) { }

        public void PublishTopic(string topic, object? payloadSummary) { }
    }

    private sealed class FakeStore : IReadOnlyStateStore
    {
        public bool TryGetOutput(string stateName, out object? output)
        {
            output = null;
            return false;
        }
    }

    private static StateContext MakeContext(IEventProvider events) =>
        new()
        {
            Events = events,
            Store = new FakeStore(),
            ExecutionId = Guid.NewGuid().ToString("D"),
            StateName = "S1",
        };

    /// <summary>sleep は duration 後に Unit を返す。</summary>
    [Fact]
    public async Task SleepActionState_WithDuration_ReturnsUnit()
    {
        // Arrange
        var state = new SleepActionState();
        var input = new Dictionary<string, object?> { ["duration"] = "20ms" };

        // Act
        var result = await state.ExecuteAsync(MakeContext(new FakeEventProvider()), input, CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, result);
    }
}
