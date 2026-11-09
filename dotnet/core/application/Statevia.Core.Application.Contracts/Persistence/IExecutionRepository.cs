namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>executions / execution_graph_snapshots 永続化。</summary>
public interface IExecutionRepository
{
    /// <summary>テナントと Execution ID で Execution の投影を取得する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>Execution の投影。無ければ <see langword="null"/>。</returns>
    Task<ExecutionRow?> GetByIdAsync(ICoreUnitOfWork uow, Guid tenantId, Guid executionId, CancellationToken ct);

    /// <summary>Execution ID だけで Execution の投影を取得する。テナントのクエリフィルタは使わない。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>Execution の投影。無ければ <see langword="null"/>。</returns>
    Task<ExecutionRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);

    /// <summary>呼び出し元 Principal が Reader 以上の定義 Project に属する実行だけをページングする。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="principalId">Principal ID。</param>
    /// <param name="query">ページングと絞り込みの条件。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>絞り込み後の総件数と、ページ内の Execution と表示用 ID。</returns>
    Task<(int TotalCount, List<(ExecutionRow Execution, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        ExecutionListPageQuery query,
        CancellationToken ct);

    /// <summary>Execution の投影と実行グラフのスナップショットをユニットオブワークに追加する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="execution">追加する Execution の投影。</param>
    /// <param name="snapshot">追加する実行グラフのスナップショット。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>追加が終わると完了するタスク。</returns>
    Task AddExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        ExecutionRow execution,
        ExecutionGraphSnapshotRow snapshot,
        CancellationToken ct);

    /// <summary>Execution の実行グラフのスナップショットを取得する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>スナップショット。無ければ <see langword="null"/>。</returns>
    Task<ExecutionGraphSnapshotRow?> GetSnapshotByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);

    /// <summary>
    /// スナップショット行の <c>UpdatedAt</c> だけを返す。行が無ければ null。
    /// </summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>行があるときの更新時刻。無ければ null。</returns>
    /// <remarks><c>GraphJson</c> は読まない。欠落を <see cref="DateTime"/> の既定値で表さない。</remarks>
    Task<DateTime?> TryGetSnapshotUpdatedAtByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);

    /// <summary>
    /// 実行行の status だけを返す。行が無ければ null。
    /// </summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>投影 status。行が無ければ null。</returns>
    /// <remarks>テナントフィルタは外さない。グラフ JSON は読まない。</remarks>
    Task<string?> TryGetExecutionStatusByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);

    /// <summary>executions の status を更新し、graph JSON が変わったときだけ snapshot を書く。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="status">投影 status。</param>
    /// <param name="cancelRequested">取消要求。null なら既存値を維持する。</param>
    /// <param name="graphJson">実行グラフ JSON。未変化なら snapshot UPDATE しない。</param>
    /// <param name="ct">キャンセル。</param>
    /// <remarks>
    /// 明示の Cancel 以外で、終端 status を <c>Running</c> に戻さない。
    /// 遅れた persist がキューの Completed を上書きするのを防ぐ。
    /// </remarks>
    /// <returns>更新が終わると完了するタスク。</returns>
    Task UpdateExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string status,
        bool? cancelRequested,
        string graphJson,
        CancellationToken ct);

    /// <summary>載荷済み試行上限の観測印として <c>restart_lost</c> を立てる。status は変えない。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>更新が終わると完了するタスク。</returns>
    Task MarkRestartLostAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
        Task.CompletedTask;
}
