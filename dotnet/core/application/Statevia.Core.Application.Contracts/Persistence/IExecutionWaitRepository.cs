namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>execution_waits 永続化。</summary>
public interface IExecutionWaitRepository
{
    /// <summary>
    /// 指定 execution の wait 行を置換する（不在 node_id を削除し、指定行を upsert）。
    /// </summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="executionId">対象 execution。</param>
    /// <param name="waits">残したい wait 行（1 Wait = 1 行）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>置換が終わると完了するタスク。</returns>
    Task ReplaceWaitsAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        IReadOnlyList<ExecutionWaitRow> waits,
        CancellationToken ct);

    /// <summary>
    /// 指定 node_id の wait 行を削除する（Resume 成功時）。
    /// </summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="executionId">対象 execution。</param>
    /// <param name="nodeId">削除する Wait ノード ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>削除が終わると完了するタスク。</returns>
    Task DeleteByNodeIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string nodeId,
        CancellationToken ct);

    /// <summary>
    /// 指定 execution の wait 行を created_at / node_id 順で返す。
    /// </summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="executionId">対象 execution。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>wait 行一覧。</returns>
    Task<IReadOnlyList<ExecutionWaitRow>> ListByExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct);

    /// <summary>期限切れの DelayWait を取得する。</summary>
    /// <param name="utcNow">現在 UTC 時刻。</param>
    /// <param name="limit">最大件数。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>期限切れの wait 行一覧。</returns>
    Task<IReadOnlyList<ExecutionWaitRow>> ListExpiredDelayWaitsAsync(
        DateTime utcNow,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// 指定テナント内で topic / key に厳密一致する購読を、判定する Principal の範囲に入るかとともに取得する。
    /// </summary>
    /// <remarks>
    /// 範囲は、購読の実行の定義の Project に実行者以上で所属し、かつその定義が操作できる定義に入ること。
    /// 実行の一覧と同じ範囲の解釈を使う。並列分岐の物理子の購読は、親と同じ定義で判定される。
    /// 範囲の外の購読も <see cref="MatchingWaitSubscription.InScope"/> が false の行として返す。
    /// </remarks>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="tenantId">照合するテナント（Query Filter に頼らない明示条件）。</param>
    /// <param name="topic">正規化済みトピック。</param>
    /// <param name="correlationKey">正規化済み key（未指定は空文字）。</param>
    /// <param name="evaluatedPrincipalId">範囲を判定する Principal。HTTP は呼び出し元、<c>event.publish</c> は発行した実行の開始者。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>topic / key が一致した購読。並びは購読の作成時刻・実行 ID・ノード ID の昇順。</returns>
    Task<IReadOnlyList<MatchingWaitSubscription>> ListMatchingSubscriptionsAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        string topic,
        string correlationKey,
        Guid evaluatedPrincipalId,
        CancellationToken ct);

    /// <summary>
    /// Principal が閲覧できる実行の購読から、一意な topic / key を昇順で返す。
    /// </summary>
    /// <remarks>
    /// 閲覧できる実行は、定義の Project に閲覧者以上で所属し、かつその定義が操作できる定義に入る実行（実行の一覧と同じ範囲）。
    /// 範囲で絞ってから上限を掛けるので、範囲の外の topic / key は上限の枠を使わない。
    /// </remarks>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="tenantId">列挙するテナント（Query Filter に頼らない明示条件）。</param>
    /// <param name="principalId">範囲を判定する呼び出し元 Principal。</param>
    /// <param name="limit">返す上限。1 以上。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>候補。同一 (topic, key) は 1 行。範囲に入る Hosted 子実行の購読も含む。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> が 1 未満。</exception>
    Task<IReadOnlyList<EventSubscriptionCandidate>> ListDistinctSubscriptionCandidatesAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        int limit,
        CancellationToken ct);
}
