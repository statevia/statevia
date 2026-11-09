namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// 実行ランタイムチェックポイントの文書ストア（キー = executionId）。
/// </summary>
/// <remarks>
/// <para>
/// Application は本ポートのみに依存する。実装は PostgreSQL テーブルでも、
/// MongoDB / DynamoDB 等のドキュメントストアでも差し替え可能とする。
/// </para>
/// <para>
/// 現行の Postgres 実装は物理テーブル <c>execution_runtime_checkpoints</c> を用いるが、
/// 契約上は RDB 行ではなく文書（document）として扱う。
/// 所有・fencing 更新は世代一致条件付き API を用いる。
/// </para>
/// </remarks>
public interface IExecutionCheckpointStore
{
    /// <summary>チェックポイント文書の runtime JSON を upsert する（既存行の所有メタは保持）。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="document">書き込むチェックポイント文書。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>upsert が終わると完了するタスク。</returns>
    Task UpsertAsync(
        ICoreUnitOfWork uow,
        ExecutionCheckpointDocument document,
        CancellationToken ct);

    /// <summary>実行 ID でチェックポイント文書を取得する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>チェックポイント文書。無ければ <see langword="null"/>。</returns>
    Task<ExecutionCheckpointDocument?> GetByExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct);

    /// <summary>チェックポイント文書を削除する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>削除が終わると完了するタスク。</returns>
    Task DeleteAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct);

    /// <summary>
    /// 所有を獲得し <see cref="ExecutionCheckpointDocument.OwnerGeneration"/> を +1 する。
    /// 文書が無ければ <paramref name="seed"/> で作成してから獲得する。
    /// </summary>
    /// <returns>獲得後の世代。失敗時は null。</returns>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="workerId">所有する Worker の ID。</param>
    /// <param name="leaseUntilUtc">lease の期限（UTC）。</param>
    /// <param name="seed">文書が無いときに作る初期文書。null なら作らない。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task<long?> TryAcquireOwnershipAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string workerId,
        DateTime leaseUntilUtc,
        ExecutionCheckpointDocument? seed,
        CancellationToken ct);

    /// <summary>
    /// 期限切れ所有を原子的に奪取し世代を +1 する（Scheduler recovery 用）。
    /// </summary>
    /// <returns>奪取後の世代。対象なしは null。</returns>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="workerId">所有する Worker の ID。</param>
    /// <param name="nowUtc">判定に使う現在時刻（UTC）。</param>
    /// <param name="newLeaseUntilUtc">奪取後の lease の期限（UTC）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task<long?> TrySeizeExpiredOwnershipAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string workerId,
        DateTime nowUtc,
        DateTime newLeaseUntilUtc,
        CancellationToken ct);

    /// <summary>世代一致時のみ lease を延長する。</summary>
    /// <returns>更新できたとき true。</returns>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="ownerGeneration">所有の世代。一致するときだけ処理する。</param>
    /// <param name="leaseUntilUtc">lease の期限（UTC）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task<bool> TryRenewOwnershipLeaseAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        long ownerGeneration,
        DateTime leaseUntilUtc,
        CancellationToken ct);

    /// <summary>世代一致時のみ runtime JSON を更新し、任意で lease を延長する。</summary>
    /// <returns>更新できたとき true。</returns>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="upsert">更新内容と、一致を求める世代。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task<bool> TryUpsertRuntimeWithGenerationAsync(
        ICoreUnitOfWork uow,
        ExecutionCheckpointRuntimeUpsert upsert,
        CancellationToken ct);

    /// <summary>世代一致時のみ所有を解放する（Unload 時）。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="ownerGeneration">所有の世代。一致するときだけ処理する。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>解放できたら <see langword="true"/>。世代が一致しなければ <see langword="false"/>。</returns>
    Task<bool> TryClearOwnershipAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        long ownerGeneration,
        CancellationToken ct);

    /// <summary>lease 期限切れかつ所有者がいる実行 ID を列挙する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="nowUtc">判定に使う現在時刻（UTC）。</param>
    /// <param name="limit">返す件数の上限。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>最大 <paramref name="limit"/> 件の Execution ID。</returns>
    Task<IReadOnlyList<Guid>> ListExpiredOwnedExecutionIdsAsync(
        ICoreUnitOfWork uow,
        DateTime nowUtc,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// 期限切れ所有を世代 +1 のうえ所有者なしにし、recovery enqueue の直前に呼ぶ。
    /// </summary>
    /// <returns>更新できたとき true。</returns>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="nowUtc">判定に使う現在時刻（UTC）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task<bool> TryBumpGenerationAndClearExpiredOwnerAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        DateTime nowUtc,
        CancellationToken ct);
}
