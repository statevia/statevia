using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.FSM;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Statevia.Core.Application.Services;

/// <summary>
/// 実行グラフ JSON と DB 行から UI 向け ExecutionView / Graph パッチ、および未完了 Wait の再開キー一覧を組み立てる。
/// </summary>
internal static class ExecutionViewMapper
{
    public static ExecutionViewDto BuildExecutionView(
        ExecutionRow execution,
        string graphJson,
        string displayId,
        string graphIdDisplay)
    {
        return new ExecutionViewDto
        {
            DisplayId = displayId,
            ResourceId = execution.ExecutionId.ToString("D"),
            GraphId = graphIdDisplay,
            Status = execution.Status,
            StartedAt = execution.StartedAt,
            UpdatedAt = execution.UpdatedAt,
            CancelRequested = execution.CancelRequested,
            RestartLost = execution.RestartLost,
            Nodes = MapNodes(graphJson)
        };
    }

    public static IReadOnlyList<ExecutionViewNodeDto> MapNodes(string graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
            return Array.Empty<ExecutionViewNodeDto>();

        if (!JsonDeserialize.TryDeserialize(graphJson, JsonSerializerProfiles.CaseInsensitive, out ExecutionGraphSnapshotDto? dto))
            return Array.Empty<ExecutionViewNodeDto>();

        if (dto?.Nodes is null || dto.Nodes.Count == 0)
            return Array.Empty<ExecutionViewNodeDto>();

        var list = new List<ExecutionViewNodeDto>(dto.Nodes.Count);
        foreach (var n in dto.Nodes)
        {
            var nodeStatus = MapNodeStatus(n);
            var canceledByExecution = n.CanceledByExecution
                ?? string.Equals(n.Fact, Fact.Cancelled, StringComparison.OrdinalIgnoreCase);
            var nodeType = ResolveNodeType(n);

            list.Add(new ExecutionViewNodeDto
            {
                NodeId = n.NodeId ?? string.Empty,
                NodeName = n.NodeName ?? string.Empty,
                NodeType = nodeType,
                Status = nodeStatus,
                Attempt = n.Attempt ?? 1,
                WorkerId = n.WorkerId,
                WaitKey = n.WaitKey,
                AllowedEvents = NormalizeAllowedEvents(n.AllowedEvents),
                CanceledByExecution = canceledByExecution,
                Input = n.Input,
                Output = n.Output,
                ConditionRouting = n.ConditionRouting
            });
        }

        return list;
    }

    /// <summary>
    /// graph スナップショット JSON から未完了 Wait だけを再開キーとして射影する。
    /// </summary>
    /// <param name="graphJson"><see cref="IExecutionService.GetGraphJsonAsync"/> と同じグラフ JSON。</param>
    /// <returns>WAITING かつ NodeType が Wait の要素。順序は <c>nodes</c> 配列順。0 件でも空の <see cref="ExecutionWaitsResponse"/>。</returns>
    /// <remarks>
    /// 機微 IO 方針: <c>input</c> / <c>output</c> は載せない。<c>allowedEvents</c> が null のときは空配列にする。
    /// </remarks>
    public static ExecutionWaitsResponse MapActiveWaits(string graphJson)
    {
        var waits = MapNodes(graphJson)
            .Where(node =>
                string.Equals(node.Status, WaitingNodeStatus, StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.NodeType, WaitNodeType, StringComparison.OrdinalIgnoreCase))
            .Select(node => new ExecutionWaitItemDto
            {
                NodeId = node.NodeId,
                NodeName = node.NodeName,
                AllowedEvents = node.AllowedEvents is { Count: > 0 }
                    ? node.AllowedEvents
                    : Array.Empty<string>()
            })
            .ToList();

        return new ExecutionWaitsResponse { Waits = waits };
    }

    public static IReadOnlyList<GraphPatchNodeDto> MapGraphPatchNodes(string graphJson) =>
        ReadGraphUpdate(graphJson).Nodes;

    /// <summary>
    /// グラフ JSON を 1 回だけ解釈し、SSE パッチと終端判定を返す。input / output は展開しない。
    /// </summary>
    /// <param name="graphJson">投影グラフ JSON。</param>
    /// <returns>パッチノードと、現行の終端条件を満たすか。</returns>
    public static GraphUpdateRead ReadGraphUpdate(string graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
            return GraphUpdateRead.Empty;

        if (!JsonDeserialize.TryDeserialize(graphJson, JsonSerializerProfiles.CaseInsensitive, out GraphPatchSnapshotDto? dto)
            || dto is null)
            return GraphUpdateRead.Empty;

        return new GraphUpdateRead(MapPatchNodes(dto), IsTerminalPatch(dto));
    }

    /// <summary>UI ノード状態。未完了の Wait は WAITING。</summary>
    private const string WaitingNodeStatus = "WAITING";

    /// <summary>UI ノード状態。完了時刻が無い、Wait 以外のノード。</summary>
    private const string RunningNodeStatus = "RUNNING";

    /// <summary>UI ノード状態。Completed、Joined、および未知の fact。</summary>
    private const string SucceededNodeStatus = "SUCCEEDED";

    /// <summary>UI ノード状態。fact が Failed のとき。</summary>
    private const string FailedNodeStatus = "FAILED";

    /// <summary>UI ノード状態。fact は Cancelled、文字列は CANCELED。</summary>
    private const string CanceledNodeStatus = "CANCELED";

    /// <summary>グラフ JSON の nodeType。未完了なら WaitingNodeStatus にする。</summary>
    private const string WaitNodeType = "Wait";

    private static readonly HashSet<string> TerminalSnapshotFacts = new(StringComparer.Ordinal)
    {
        "Completed",
        "Cancelled",
        "Failed"
    };

    private static IReadOnlyList<GraphPatchNodeDto> MapPatchNodes(GraphPatchSnapshotDto dto)
    {
        if (dto.Nodes is not { Count: > 0 })
            return Array.Empty<GraphPatchNodeDto>();

        var list = new List<GraphPatchNodeDto>(dto.Nodes.Count);
        foreach (var node in dto.Nodes)
        {
            var canceledByExecution = node.CanceledByExecution
                ?? string.Equals(node.Fact, Fact.Cancelled, StringComparison.OrdinalIgnoreCase);
            list.Add(new GraphPatchNodeDto
            {
                NodeId = node.NodeId ?? string.Empty,
                NodeName = string.IsNullOrWhiteSpace(node.NodeName) ? null : node.NodeName,
                Status = MapPatchNodeStatus(node),
                Attempt = node.Attempt ?? 1,
                WorkerId = node.WorkerId,
                WaitKey = node.WaitKey,
                AllowedEvents = NormalizeAllowedEvents(node.AllowedEvents),
                CanceledByExecution = canceledByExecution
            });
        }

        return list;
    }

    private static string MapPatchNodeStatus(GraphPatchSourceNodeDto node)
    {
        if (node.CompletedAt is null)
        {
            return string.Equals(node.NodeType, WaitNodeType, StringComparison.OrdinalIgnoreCase)
                ? WaitingNodeStatus
                : RunningNodeStatus;
        }

        return node.Fact switch
        {
            Fact.Completed => SucceededNodeStatus,
            Fact.Failed => FailedNodeStatus,
            Fact.Cancelled => CanceledNodeStatus,
            Fact.Joined => SucceededNodeStatus,
            _ => SucceededNodeStatus
        };
    }

    private static bool IsTerminalPatch(GraphPatchSnapshotDto dto)
    {
        if (dto.Nodes is not { Count: > 0 })
            return false;

        var sinkNodeIds = GetPatchSinkNodeIds(dto);
        foreach (var node in dto.Nodes)
        {
            if (node.Fact is not null && TerminalSnapshotFacts.Contains(node.Fact))
                return true;

            if (!string.IsNullOrWhiteSpace(node.NodeId)
                && sinkNodeIds.Contains(node.NodeId)
                && node.CompletedAt is not null)
                return true;
        }

        return false;
    }

    private static HashSet<string> GetPatchSinkNodeIds(GraphPatchSnapshotDto dto)
    {
        var nodeIds = dto.Nodes!
            .Select(node => node.NodeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);

        if (dto.Edges is not { Count: > 0 })
            return nodeIds;

        nodeIds.ExceptWith(
            dto.Edges
                .Select(edge => edge.From)
                .Where(from => !string.IsNullOrWhiteSpace(from))
                .Select(from => from!));

        return nodeIds;
    }

    /// <summary>SSE 向けに 1 回解釈したパッチと終端判定。</summary>
    /// <param name="Nodes">公開パッチのノード。</param>
    /// <param name="IsTerminal">現行の終端条件を満たすとき true。</param>
    public readonly record struct GraphUpdateRead(IReadOnlyList<GraphPatchNodeDto> Nodes, bool IsTerminal)
    {
        /// <summary>空または解釈できない JSON。</summary>
        public static GraphUpdateRead Empty { get; } = new(Array.Empty<GraphPatchNodeDto>(), false);
    }

    private sealed class GraphPatchSnapshotDto
    {
        [JsonPropertyName("nodes")]
        public List<GraphPatchSourceNodeDto>? Nodes { get; set; }

        [JsonPropertyName("edges")]
        public List<GraphPatchEdgeDto>? Edges { get; set; }
    }

    private sealed class GraphPatchEdgeDto
    {
        [JsonPropertyName("from")]
        public string? From { get; set; }
    }

    private sealed class GraphPatchSourceNodeDto
    {
        [JsonPropertyName("nodeId")]
        public string? NodeId { get; set; }

        [JsonPropertyName("nodeName")]
        public string? NodeName { get; set; }

        [JsonPropertyName("nodeType")]
        public string? NodeType { get; set; }

        [JsonPropertyName("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [JsonPropertyName("fact")]
        public string? Fact { get; set; }

        [JsonPropertyName("attempt")]
        public int? Attempt { get; set; }

        [JsonPropertyName("workerId")]
        public string? WorkerId { get; set; }

        [JsonPropertyName("waitKey")]
        public string? WaitKey { get; set; }

        [JsonPropertyName("allowedEvents")]
        public List<string>? AllowedEvents { get; set; }

        [JsonPropertyName("canceledByExecution")]
        public bool? CanceledByExecution { get; set; }
    }

    /// <summary>空・空白のみを除き、前後空白を Trim した許可イベント一覧を返す（空なら null）。</summary>
    private static List<string>? NormalizeAllowedEvents(List<string>? allowedEvents)
    {
        if (allowedEvents is null || allowedEvents.Count == 0)
            return null;

        var normalized = allowedEvents
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count == 0 ? null : normalized;
    }

    /// <summary>
    /// グラフノードの UI 向け status を解決する。
    /// 未完了 Wait は WAITING（Resume 可否と仕様の NodeStatus に合わせる）。
    /// </summary>
    private static string MapNodeStatus(ExecutionNodeDto node)
    {
        if (node.CompletedAt is null)
        {
            return string.Equals(node.NodeType, WaitNodeType, StringComparison.OrdinalIgnoreCase)
                ? WaitingNodeStatus
                : RunningNodeStatus;
        }

        return node.Fact switch
        {
            Fact.Completed => SucceededNodeStatus,
            Fact.Failed => FailedNodeStatus,
            Fact.Cancelled => CanceledNodeStatus,
            Fact.Joined => SucceededNodeStatus,
            _ => SucceededNodeStatus
        };
    }

    private static string ResolveNodeType(ExecutionNodeDto node)
    {
        if (!string.IsNullOrWhiteSpace(node.NodeType))
            return node.NodeType;
        return "Task";
    }

    private sealed class ExecutionGraphSnapshotDto
    {
        [JsonPropertyName("nodes")]
        public List<ExecutionNodeDto>? Nodes { get; set; }
    }

    private sealed class ExecutionNodeDto
    {
        [JsonPropertyName("nodeId")]
        public string? NodeId { get; set; }

        [JsonPropertyName("nodeName")]
        public string? NodeName { get; set; }

        [JsonPropertyName("nodeType")]
        public string? NodeType { get; set; }

        [JsonPropertyName("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonPropertyName("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [JsonPropertyName("fact")]
        public string? Fact { get; set; }

        [JsonPropertyName("input")]
        public JsonElement? Input { get; set; }

        [JsonPropertyName("output")]
        public JsonElement? Output { get; set; }

        [JsonPropertyName("attempt")]
        public int? Attempt { get; set; }

        [JsonPropertyName("workerId")]
        public string? WorkerId { get; set; }

        [JsonPropertyName("waitKey")]
        public string? WaitKey { get; set; }

        [JsonPropertyName("allowedEvents")]
        public List<string>? AllowedEvents { get; set; }

        [JsonPropertyName("canceledByExecution")]
        public bool? CanceledByExecution { get; set; }

        [JsonPropertyName("conditionRouting")]
        public JsonElement? ConditionRouting { get; set; }
    }
}
