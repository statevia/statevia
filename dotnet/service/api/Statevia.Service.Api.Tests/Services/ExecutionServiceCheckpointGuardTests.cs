using Statevia.Core.Application.Services;
using Statevia.Core.Engine.Abstractions;
using System.Text.Json;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>Checkpoint / ForkJoin の静的ガードヘルパー（ドメインサービス）。</summary>
public sealed class ExecutionServiceCheckpointGuardTests
{
    /// <summary>終端 incoming は遅れ判定しない。</summary>
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void IsRuntimeCheckpointLessAdvanced_WhenIncomingTerminal_ReturnsFalse(
        bool completed,
        bool cancelled,
        bool failed)
    {
        // Arrange
        var incoming = CreateCheckpoint(
            isCompleted: completed,
            isCancelled: cancelled,
            isFailed: failed,
            nodeCount: 1);
        var stored = CreateCheckpoint(nodeCount: 3);

        // Act
        var lessAdvanced = ExecutionCheckpointService.IsRuntimeCheckpointLessAdvanced(incoming, stored);

        // Assert
        Assert.False(lessAdvanced);
    }

    /// <summary>ノード数が少ない incoming は遅れている。</summary>
    [Fact]
    public void IsRuntimeCheckpointLessAdvanced_WhenFewerNodes_ReturnsTrue()
    {
        // Arrange
        var incoming = CreateCheckpoint(nodeCount: 1);
        var stored = CreateCheckpoint(nodeCount: 3);

        // Act
        var lessAdvanced = ExecutionCheckpointService.IsRuntimeCheckpointLessAdvanced(incoming, stored);

        // Assert
        Assert.True(lessAdvanced);
    }

    /// <summary>stored に Wait があり incoming が空なら遅れている。</summary>
    [Fact]
    public void IsRuntimeCheckpointLessAdvanced_WhenStoredHasWaitAndIncomingIdle_ReturnsTrue()
    {
        // Arrange
        var incoming = CreateCheckpoint(nodeCount: 2);
        var stored = CreateCheckpoint(
            nodeCount: 2,
            pendingWaits:
            [
                new CheckpointPendingWait
                {
                    NodeId = "w1",
                    NodeName = "Wait1",
                    AllowedEvents = ["go"]
                }
            ]);

        // Act
        var lessAdvanced = ExecutionCheckpointService.IsRuntimeCheckpointLessAdvanced(incoming, stored);

        // Assert
        Assert.True(lessAdvanced);
    }

    /// <summary>未完了の Wait ノード向け Persist（Wait Suspend）は Unload 廃止判定しない。</summary>
    [Fact]
    public void IsForkExpansionUnloadObsolete_WhenNotForkNode_ReturnsFalse()
    {
        // Arrange
        var checkpoint = CreateCheckpoint(
            nodes:
            [
                new CheckpointGraphNode
                {
                    NodeId = "wait1",
                    NodeName = "Wait1",
                    NodeType = "Wait",
                    StartedAt = DateTime.UtcNow
                }
            ],
            activeStates: ["Decide1"]);

        // Act
        var obsolete = ExecutionCheckpointService.IsForkExpansionUnloadObsolete(checkpoint, "wait1");

        // Assert
        Assert.False(obsolete);
    }

    /// <summary>Fork 後に Active / Wait があれば Unload は廃止。</summary>
    [Fact]
    public void IsForkExpansionUnloadObsolete_WhenActiveAfterFork_ReturnsTrue()
    {
        // Arrange
        var started = DateTime.UtcNow;
        var checkpoint = CreateCheckpoint(
            nodes:
            [
                new CheckpointGraphNode
                {
                    NodeId = "fork1",
                    NodeName = "Fork1",
                    NodeType = "Fork",
                    StartedAt = started,
                    CompletedAt = started,
                    Fact = "Completed"
                }
            ],
            activeStates: ["Decide1"]);

        // Act
        var obsolete = ExecutionCheckpointService.IsForkExpansionUnloadObsolete(checkpoint, "fork1");

        // Assert
        Assert.True(obsolete);
    }

    /// <summary>Fork 以降に Join 完了があれば Unload は廃止。</summary>
    [Fact]
    public void IsForkExpansionUnloadObsolete_WhenJoinCompletedAfterFork_ReturnsTrue()
    {
        // Arrange
        var forkStarted = DateTime.UtcNow;
        var checkpoint = CreateCheckpoint(
            nodes:
            [
                new CheckpointGraphNode
                {
                    NodeId = "fork1",
                    NodeName = "Fork1",
                    NodeType = "Fork",
                    StartedAt = forkStarted,
                    CompletedAt = forkStarted,
                    Fact = "Completed"
                },
                new CheckpointGraphNode
                {
                    NodeId = "join1",
                    NodeName = "Join1",
                    NodeType = "Join",
                    StartedAt = forkStarted.AddMilliseconds(1),
                    CompletedAt = forkStarted.AddMilliseconds(2),
                    Fact = "Joined"
                }
            ]);

        // Act
        var obsolete = ExecutionCheckpointService.IsForkExpansionUnloadObsolete(checkpoint, "fork1");

        // Assert
        Assert.True(obsolete);
    }

    /// <summary>
    /// 初期 State（種別 Start）から Fork した親でも、Fork 以降に Join が完了していれば Unload は廃止。
    /// </summary>
    /// <remarks>
    /// 親の復元で Fork 展開の通知が再発火し、Join → End の途中で Running の断面を書いて Unload すると、
    /// Execution の Completed が投影されずに残る（ACTIVE のまま）。種別が Fork でなくても止める。
    /// </remarks>
    [Fact]
    public void IsForkExpansionUnloadObsolete_WhenStartNodeForkedAndJoinCompleted_ReturnsTrue()
    {
        // Arrange
        var forkStarted = DateTime.UtcNow;
        var checkpoint = CreateCheckpoint(
            nodes:
            [
                new CheckpointGraphNode
                {
                    NodeId = "start1",
                    NodeName = "Start",
                    NodeType = "Start",
                    StartedAt = forkStarted,
                    CompletedAt = forkStarted,
                    Fact = "Completed"
                },
                new CheckpointGraphNode
                {
                    NodeId = "join1",
                    NodeName = "Join1",
                    NodeType = "Join",
                    StartedAt = forkStarted.AddSeconds(1),
                    CompletedAt = forkStarted.AddSeconds(1),
                    Fact = "Joined"
                },
                new CheckpointGraphNode
                {
                    NodeId = "end1",
                    NodeName = "End",
                    NodeType = "End",
                    StartedAt = forkStarted.AddSeconds(1),
                    CompletedAt = forkStarted.AddSeconds(1),
                    Fact = "Completed"
                }
            ]);

        // Act
        var obsolete = ExecutionCheckpointService.IsForkExpansionUnloadObsolete(checkpoint, "start1");

        // Assert
        Assert.True(obsolete);
    }

    /// <summary>Wait から Fork した親で、後続 State の実行中なら Unload は廃止。</summary>
    [Fact]
    public void IsForkExpansionUnloadObsolete_WhenWaitNodeForkedAndActiveAfterFork_ReturnsTrue()
    {
        // Arrange
        var started = DateTime.UtcNow;
        var checkpoint = CreateCheckpoint(
            nodes:
            [
                new CheckpointGraphNode
                {
                    NodeId = "wait1",
                    NodeName = "Wait1",
                    NodeType = "Wait",
                    StartedAt = started,
                    CompletedAt = started,
                    Fact = "Completed"
                }
            ],
            activeStates: ["End"]);

        // Act
        var obsolete = ExecutionCheckpointService.IsForkExpansionUnloadObsolete(checkpoint, "wait1");

        // Assert
        Assert.True(obsolete);
    }

    /// <summary>Join fact=Joined なら既完了。</summary>
    [Fact]
    public void IsPhysicalJoinAlreadyCompleted_WhenFactJoined_ReturnsTrue()
    {
        // Arrange
        const string graphJson = """
            {
              "nodes": [
                {"nodeId":"fork1","nodeName":"Fork1","nodeType":"Fork"},
                {"nodeId":"join1","nodeName":"Join1","nodeType":"Join","fact":"Joined"}
              ],
              "edges": []
            }
            """;

        // Act
        var completed = ExecutionForkJoinCoordinator.IsPhysicalJoinAlreadyCompleted(graphJson, "fork1", "Join1");

        // Assert
        Assert.True(completed);
    }

    /// <summary>Join に completedAt があれば既完了。</summary>
    [Fact]
    public void IsPhysicalJoinAlreadyCompleted_WhenCompletedAtPresent_ReturnsTrue()
    {
        // Arrange
        const string graphJson = """
            {
              "nodes": [
                {"nodeId":"fork1","nodeName":"Fork1","nodeType":"Fork"},
                {"nodeId":"join1","nodeName":"Join1","nodeType":"Join","completedAt":"2026-01-01T00:00:00Z"}
              ],
              "edges": []
            }
            """;

        // Act
        var completed = ExecutionForkJoinCoordinator.IsPhysicalJoinAlreadyCompleted(graphJson, "fork1", "Join1");

        // Assert
        Assert.True(completed);
    }

    /// <summary>Join status=SUCCEEDED なら既完了。</summary>
    [Fact]
    public void IsPhysicalJoinAlreadyCompleted_WhenStatusSucceeded_ReturnsTrue()
    {
        // Arrange
        const string graphJson = """
            {
              "nodes": [
                {"nodeId":"fork1","nodeName":"Fork1","nodeType":"Fork"},
                {"nodeId":"join1","nodeName":"Join1","nodeType":"Join","status":"SUCCEEDED"}
              ],
              "edges": []
            }
            """;

        // Act
        var completed = ExecutionForkJoinCoordinator.IsPhysicalJoinAlreadyCompleted(graphJson, "fork1", "Join1");

        // Assert
        Assert.True(completed);
    }

    /// <summary>未解決 Join は未完了。</summary>
    [Fact]
    public void IsPhysicalJoinAlreadyCompleted_WhenJoinUnresolved_ReturnsFalse()
    {
        // Arrange
        const string graphJson = """
            {
              "nodes": [
                {"nodeId":"fork1","nodeName":"Fork1","nodeType":"Fork"}
              ],
              "edges": []
            }
            """;

        // Act
        var completed = ExecutionForkJoinCoordinator.IsPhysicalJoinAlreadyCompleted(graphJson, "fork1", "Join1");

        // Assert
        Assert.False(completed);
    }

    /// <summary>不正 JSON は未完了扱い。</summary>
    [Fact]
    public void IsPhysicalJoinAlreadyCompleted_WhenInvalidJson_ReturnsFalse()
    {
        // Arrange / Act
        var completed = ExecutionForkJoinCoordinator.IsPhysicalJoinAlreadyCompleted("{not-json", "fork1", "Join1");

        // Assert
        Assert.False(completed);
    }

    private static ExecutionRuntimeCheckpoint CreateCheckpoint(
        int nodeCount = 0,
        bool isCompleted = false,
        bool isCancelled = false,
        bool isFailed = false,
        IReadOnlyList<string>? activeStates = null,
        IReadOnlyList<CheckpointPendingWait>? pendingWaits = null,
        IReadOnlyList<CheckpointGraphNode>? nodes = null)
    {
        var graphNodes = nodes
            ?? Enumerable.Range(0, nodeCount)
                .Select(i => new CheckpointGraphNode
                {
                    NodeId = $"n{i}",
                    NodeName = $"N{i}",
                    NodeType = "Action",
                    StartedAt = DateTime.UtcNow
                })
                .ToArray();

        return new ExecutionRuntimeCheckpoint
        {
            ExecutionId = "exec-1",
            DefinitionName = "def",
            IsCompleted = isCompleted,
            IsCancelled = isCancelled,
            IsFailed = isFailed,
            ActiveStates = activeStates ?? [],
            StateAttempts = new Dictionary<string, int>(StringComparer.Ordinal),
            StateOutputs = new Dictionary<string, JsonElement?>(StringComparer.Ordinal),
            AppliedPublishClientEventIds = [],
            AppliedCancelClientEventIds = [],
            Context = new CheckpointContextData
            {
                States = new Dictionary<string, JsonElement?>(StringComparer.Ordinal)
            },
            Graph = new CheckpointGraphData
            {
                Nodes = graphNodes,
                Edges = []
            },
            Join = new CheckpointJoinData
            {
                JoinStateResults = new Dictionary<string, IReadOnlyDictionary<string, CheckpointJoinObserved>>(
                    StringComparer.OrdinalIgnoreCase),
                JoinSourceNodeIds = new Dictionary<string, IReadOnlyDictionary<string, string>>(
                    StringComparer.OrdinalIgnoreCase),
                StartedJoins = []
            },
            PendingWaits = pendingWaits ?? []
        };
    }
}
