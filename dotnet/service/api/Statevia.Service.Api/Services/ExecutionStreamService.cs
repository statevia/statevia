using Statevia.Core.Application.Infrastructure;
using System.Text.Json;

namespace Statevia.Service.Api.Services;

/// <summary>
/// GET …/stream 用の SSE。snapshot の <c>UpdatedAt</c> が変わったときだけグラフ JSON を読み、<c>GraphUpdated</c> を 1 件書く。接続を閉じるのは実行 status が終端のときだけ。
/// </summary>
public sealed class ExecutionStreamService
{
    /// <summary>
    /// 投影グラフ取得のポーリング間隔（ミリ秒）。
    /// </summary>
    internal const int GraphPollingIntervalMilliseconds = 2000;

    private readonly IExecutionService _executions;
    private readonly IDisplayIdService _displayIds;

    /// <summary>
    /// <see cref="ExecutionStreamService"/> を生成する。
    /// </summary>
    /// <param name="executions">実行サービス。</param>
    /// <param name="displayIds">表示 ID 解決。</param>
    public ExecutionStreamService(IExecutionService executions, IDisplayIdService displayIds)
    {
        _executions = executions;
        _displayIds = displayIds;
    }

    /// <summary>
    /// Server-Sent Events としてグラフ JSON の変化を書き込む。
    /// </summary>
    /// <param name="response">HTTP レスポンス。</param>
    /// <param name="idOrUuid">ワークフロー表示 ID または UUID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async Task WriteStreamAsync(HttpResponse response, string idOrUuid, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (ct.IsCancellationRequested)
            return;

        var uuid = await _displayIds.ResolveAsync("execution", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // 接続開始時に tenant + execution の存在を一度だけ確認する。
        await _executions.EnsureExecutionExistsAsync(uuid.Value, ct).ConfigureAwait(false);

        var displayId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false) ?? idOrUuid;

        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache, no-transform";
        response.Headers.Connection = "keep-alive";
        response.Headers["X-Accel-Buffering"] = "no";

        var jsonOpts = JsonSerializerProfiles.CamelCase;
        DateTime? lastSentUpdatedAt = null;

        while (!ct.IsCancellationRequested)
        {
            var step = await PollStreamOnceAsync(response, uuid.Value, displayId, jsonOpts, lastSentUpdatedAt, ct).ConfigureAwait(false);
            if (step.Stop)
                return;

            lastSentUpdatedAt = step.LastSentUpdatedAt;
            if (step.Delay && !await DelayNextPollAsync(response, ct).ConfigureAwait(false))
                return;
        }
    }


    /// <summary>
    /// 1 周のポーリング結果。読み取り失敗で既に待った周は <see cref="Delay"/> を使わない。
    /// </summary>
    /// <param name="Stop">接続を終える。</param>
    /// <param name="Delay">次の周までポーリング間隔を待つ。</param>
    /// <param name="LastSentUpdatedAt">送信に成功したスナップショットの更新時刻。失敗周は前回のまま。</param>
    private readonly record struct StreamPollStep(bool Stop, bool Delay, DateTime? LastSentUpdatedAt)
    {
        /// <summary>接続を終える。</summary>
        public static StreamPollStep End(DateTime? lastSentUpdatedAt) => new(true, false, lastSentUpdatedAt);

        /// <summary>待たずに次の周へ進む。読み取り側で既に待っている。</summary>
        public static StreamPollStep Again(DateTime? lastSentUpdatedAt) => new(false, false, lastSentUpdatedAt);

        /// <summary>ポーリング間隔を待ってから次の周へ進む。</summary>
        public static StreamPollStep Wait(DateTime? lastSentUpdatedAt) => new(false, true, lastSentUpdatedAt);
    }

    /// <summary>
    /// スナップショットの更新時刻と実行 status を読み、変わったときだけグラフを送る。
    /// </summary>
    /// <param name="response">SSE 応答。</param>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="displayId">クライアントへ書く表示 ID。</param>
    /// <param name="jsonOpts">JSON シリアライズ設定。</param>
    /// <param name="lastSentUpdatedAt">直前に送信したスナップショットの更新時刻。未送信なら null。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>次の周の動き。</returns>
    private async Task<StreamPollStep> PollStreamOnceAsync(
        HttpResponse response,
        Guid executionId,
        string displayId,
        JsonSerializerOptions jsonOpts,
        DateTime? lastSentUpdatedAt,
        CancellationToken ct)
    {
        var updatedAtResult = await TryGetSnapshotUpdatedAtAsync(response, executionId, ct).ConfigureAwait(false);
        if (!updatedAtResult.ShouldContinue)
            return StreamPollStep.End(lastSentUpdatedAt);
        if (updatedAtResult.UpdatedAt is null)
            return StreamPollStep.Again(lastSentUpdatedAt);

        var statusResult = await TryGetExecutionStatusAsync(response, executionId, ct).ConfigureAwait(false);
        if (!statusResult.ShouldContinue)
            return StreamPollStep.End(lastSentUpdatedAt);
        if (statusResult.Status is null)
            return StreamPollStep.Again(lastSentUpdatedAt);

        var observedUpdatedAt = updatedAtResult.UpdatedAt.Value;
        var executionTerminal = ExecutionProjectionStatuses.IsTerminal(statusResult.Status);
        if (lastSentUpdatedAt == observedUpdatedAt)
        {
            return executionTerminal
                ? StreamPollStep.End(lastSentUpdatedAt)
                : StreamPollStep.Wait(lastSentUpdatedAt);
        }

        var snapshotResult = await TryGetSnapshotGraphJsonAsync(response, executionId, ct).ConfigureAwait(false);
        if (!snapshotResult.ShouldContinue)
            return StreamPollStep.End(lastSentUpdatedAt);
        if (snapshotResult.GraphJson is null)
            return StreamPollStep.Again(lastSentUpdatedAt);

        if (!await ProcessGraphUpdateAsync(response, snapshotResult.GraphJson, displayId, jsonOpts, ct).ConfigureAwait(false))
            return StreamPollStep.End(lastSentUpdatedAt);

        return executionTerminal
            ? StreamPollStep.End(observedUpdatedAt)
            : StreamPollStep.Wait(observedUpdatedAt);
    }

    private static bool IsStreamCancellation(HttpResponse response, CancellationToken ct) =>
        ct.IsCancellationRequested || response.HttpContext.RequestAborted.IsCancellationRequested;

    private static async Task<bool> DelayNextPollAsync(HttpResponse response, CancellationToken ct)
    {
        try
        {
            await Task.Delay(GraphPollingIntervalMilliseconds, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (IsStreamCancellation(response, ct))
        {
            return false;
        }
    }

    private async Task<(bool ShouldContinue, DateTime? UpdatedAt)> TryGetSnapshotUpdatedAtAsync(
        HttpResponse response,
        Guid executionId,
        CancellationToken ct)
    {
        try
        {
            var updatedAt = await _executions.TryGetSnapshotUpdatedAtByExecutionIdAsync(executionId, ct).ConfigureAwait(false);
            if (updatedAt is null)
                return (false, null);

            return (true, updatedAt);
        }
        catch (OperationCanceledException) when (IsStreamCancellation(response, ct))
        {
            return (false, null);
        }
#pragma warning disable CA1031 // SSE ポーリング: 時刻読み取りの失敗では接続を切らず、観測済みも更新しない
        catch (Exception)
        {
            var canContinue = await DelayNextPollAsync(response, ct).ConfigureAwait(false);
            return (canContinue, null);
        }
#pragma warning restore CA1031
    }

    private async Task<(bool ShouldContinue, string? Status)> TryGetExecutionStatusAsync(
        HttpResponse response,
        Guid executionId,
        CancellationToken ct)
    {
        try
        {
            var status = await _executions.TryGetExecutionStatusByExecutionIdAsync(executionId, ct).ConfigureAwait(false);
            if (status is null)
                return (false, null);

            return (true, status);
        }
        catch (OperationCanceledException) when (IsStreamCancellation(response, ct))
        {
            return (false, null);
        }
#pragma warning disable CA1031 // SSE ポーリング: status 読み取りの失敗では接続を切らず、観測済みも更新しない
        catch (Exception)
        {
            var canContinue = await DelayNextPollAsync(response, ct).ConfigureAwait(false);
            return (canContinue, null);
        }
#pragma warning restore CA1031
    }

    private async Task<(bool ShouldContinue, string? GraphJson)> TryGetSnapshotGraphJsonAsync(HttpResponse response, Guid executionId, CancellationToken ct)
    {
        try
        {
            var snapshotGraphJson = await _executions.TryGetSnapshotGraphJsonByExecutionIdAsync(executionId, ct).ConfigureAwait(false);
            if (snapshotGraphJson is null)
            {
                // スナップショット行が消えた場合はストリームを終了する。
                return (false, null);
            }
            return (true, snapshotGraphJson);
        }
        catch (OperationCanceledException) when (IsStreamCancellation(response, ct))
        {
            return (false, null);
        }
#pragma warning disable CA1031 // SSE ポーリング: DB／実行時の未取得例外でも接続維持のためポーリングを継続する
        catch (Exception)
        {
            var canContinue = await DelayNextPollAsync(response, ct).ConfigureAwait(false);
            return (canContinue, null);
        }
#pragma warning restore CA1031
    }

    private static async Task<bool> TryWriteAsync(HttpResponse response, string payload, CancellationToken ct)
    {
        try
        {
            await response.WriteAsync($"data: {payload}\n\n", ct).ConfigureAwait(false);
            await response.Body.FlushAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (IsStreamCancellation(response, ct))
        {
            return false;
        }
        catch (IOException) when (response.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task<bool> ProcessGraphUpdateAsync(
        HttpResponse response,
        string graphJson,
        string displayId,
        JsonSerializerOptions jsonOpts,
        CancellationToken ct)
    {
        var update = ExecutionViewMapper.ReadGraphUpdate(graphJson);
        var payload = JsonSerializer.Serialize(
            new
            {
                type = "GraphUpdated",
                executionId = displayId,
                patch = new { nodes = update.Nodes }
            },
            jsonOpts);

        return await TryWriteAsync(response, payload, ct).ConfigureAwait(false);
    }
}
