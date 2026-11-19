using System.Text.Json;

namespace Statevia.Service.Api.Scenario.Tests.Infrastructure;

/// <summary>1 件の未完了 Wait の観測値。</summary>
/// <param name="NodeId">実行グラフの opaque ノード ID。</param>
/// <param name="NodeName">定義上の状態名。</param>
/// <param name="AllowedEvents">許可イベント名。</param>
public sealed record ObservedWait(string NodeId, string NodeName, IReadOnlyList<string> AllowedEvents);

/// <summary>グラフ上の 1 ノードの観測値。</summary>
/// <param name="NodeId">実行グラフの opaque ノード ID。</param>
/// <param name="NodeName">定義上の状態名。</param>
/// <param name="Status">ノードの状態文字列（グラフ JSON の <c>status</c>）。</param>
public sealed record ObservedGraphNode(string NodeId, string NodeName, string Status);

/// <summary>
/// ある時点の Execution を GET 4 種（execution / graph / waits / events）で観測した結果。
/// </summary>
/// <remarks>
/// <para>収束待ちの述語と、タイムアウト時の失敗メッセージの両方に使う。</para>
/// <para>input / output とトークンは保持しない（機微 IO 方針）。</para>
/// </remarks>
/// <param name="Status">実行ステータス（契約の値）。</param>
/// <param name="CancelRequested">キャンセル要求フラグ。</param>
/// <param name="Nodes">グラフ上のノード。</param>
/// <param name="Waits">未完了 Wait の一覧。</param>
/// <param name="EventTypes">event_store 由来のイベント type の列（seq 順）。</param>
public sealed record ExecutionSnapshot(
    string Status,
    bool CancelRequested,
    IReadOnlyList<ObservedGraphNode> Nodes,
    IReadOnlyList<ObservedWait> Waits,
    IReadOnlyList<string> EventTypes)
{
    /// <summary>失敗メッセージ用の 1 行要約を返す（status・cancelRequested・waits 件数・イベント type 列・ノード状態）。</summary>
    public string Describe() =>
        $"status={Status}, cancelRequested={CancelRequested}, waits={Waits.Count}, " +
        $"nodes=[{string.Join(", ", Nodes.Select(node => $"{node.NodeName}:{node.Status}"))}], " +
        $"events=[{string.Join(", ", EventTypes)}]";
}

/// <summary>
/// GET の観測を繰り返し、述語が成り立つまで待つ収束 poll ヘルパ。
/// </summary>
/// <remarks>
/// <para>固定 Sleep で収束を擬装しない。成り立てば即座に返し、タイムアウトまでに成り立たなければ最後の観測を載せて失敗する。</para>
/// <para>Worker の空 claim 間隔が 1 秒固定のため、タイムアウトの既定は 15 秒とする。</para>
/// </remarks>
public static class ExecutionSettleProbe
{
    /// <summary>タイムアウトの既定値（15 秒）。</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>GET 4 種を 1 回観測して <see cref="ExecutionSnapshot"/> を返す。</summary>
    /// <param name="client">認証済み <see cref="HttpClient"/>。</param>
    /// <param name="executionId">実行 display ID。</param>
    /// <returns>観測結果。</returns>
    public static async Task<ExecutionSnapshot> ObserveAsync(HttpClient client, string executionId)
    {
        ArgumentNullException.ThrowIfNull(client);
        var escaped = Uri.EscapeDataString(executionId);

        using var execution = await GetJsonAsync(client, $"/v1/executions/{escaped}");
        using var graph = await GetJsonAsync(client, $"/v1/executions/{escaped}/graph");
        using var waits = await GetJsonAsync(client, $"/v1/executions/{escaped}/waits");
        using var events = await GetJsonAsync(client, $"/v1/executions/{escaped}/events");

        var nodes = graph.RootElement.TryGetProperty("nodes", out var nodesElement)
            ? nodesElement.EnumerateArray()
                .Select(node => new ObservedGraphNode(
                    ReadString(node, "nodeId"),
                    ReadString(node, "nodeName"),
                    ReadString(node, "status")))
                .ToList()
            : [];

        var observedWaits = waits.RootElement.GetProperty("waits").EnumerateArray()
            .Select(wait => new ObservedWait(
                ReadString(wait, "nodeId"),
                ReadString(wait, "nodeName"),
                wait.GetProperty("allowedEvents").EnumerateArray().Select(item => item.GetString() ?? "").ToList()))
            .ToList();

        var eventTypes = events.RootElement.GetProperty("events").EnumerateArray()
            .Select(item => ReadString(item, "type"))
            .ToList();

        return new ExecutionSnapshot(
            execution.RootElement.GetProperty("status").GetString() ?? "",
            execution.RootElement.GetProperty("cancelRequested").GetBoolean(),
            nodes,
            observedWaits,
            eventTypes);
    }

    /// <summary>述語が成り立つまで観測を繰り返す。</summary>
    /// <param name="client">認証済み <see cref="HttpClient"/>。</param>
    /// <param name="executionId">実行 display ID。</param>
    /// <param name="predicate">収束条件。</param>
    /// <param name="description">失敗メッセージに出す収束条件の説明。</param>
    /// <param name="timeout">タイムアウト（省略時は <see cref="DefaultTimeout"/>）。</param>
    /// <returns>述語が成り立った最初の観測。</returns>
    /// <exception cref="TimeoutException">タイムアウトまでに述語が成り立たなかった場合。メッセージに最後の観測を含む。</exception>
    public static async Task<ExecutionSnapshot> WaitUntilAsync(
        HttpClient client,
        string executionId,
        Func<ExecutionSnapshot, bool> predicate,
        string description,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var effectiveTimeout = timeout ?? DefaultTimeout;
        var deadline = DateTime.UtcNow + effectiveTimeout;
        ExecutionSnapshot? last = null;

        while (true)
        {
            last = await ObserveAsync(client, executionId);
            if (predicate(last))
                return last;
            if (DateTime.UtcNow >= deadline)
                break;
            await Task.Delay(PollInterval);
        }

        throw new TimeoutException(
            $"実行 {executionId} が {effectiveTimeout} 以内に収束しませんでした（待った条件: {description}）。最後の観測: {last.Describe()}");
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
