namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// 実行の要約の読み取り（件数、定義ごとの最後の実行、1 つの実行の待機中かどうか）。
/// </summary>
/// <remarks>
/// <para>数えるのはルートの実行だけ（物理子 Execution は除く）。範囲は実行の一覧と同じ（テナント、Reader 以上の Project、実行リソース許可）。</para>
/// <para>
/// 待機中は、自身または子孫の物理子 Execution が <c>EventWait</c> / <c>CallbackWait</c> を持つ <c>Running</c> の実行。
/// 要対応は、待機中または <c>Failed</c> の実行。<c>Failed</c> は期間で区切らない。
/// </para>
/// </remarks>
public interface IExecutionSummaryRepository
{
    /// <summary>範囲と絞り込みの中で、状態ごと・待機中・要対応の件数を 1 回の集計で数える。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID。</param>
    /// <param name="principalId">呼び出し元 Principal。</param>
    /// <param name="filter">絞り込み。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>件数。対象が無ければすべて 0。</returns>
    Task<ExecutionCounts> CountAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        ExecutionCountFilter filter,
        CancellationToken ct);

    /// <summary>定義 ID ごとに、全版を通して開始日時がいちばん新しいルートの実行を返す。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID。</param>
    /// <param name="definitionIds">対象の定義 UUID。呼び出し側で範囲（Project と実行リソース許可）を絞ってあること。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>定義 UUID をキーにした最後の実行。実行の無い定義はキーを持たない。</returns>
    /// <remarks>定義の件数によらず決まった回数のクエリで返す（N+1 にしない）。同じ開始日時は実行 ID の大きいものを選ぶ。</remarks>
    Task<IReadOnlyDictionary<Guid, LatestRootExecution>> ListLatestRootByDefinitionIdsAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        IReadOnlyCollection<Guid> definitionIds,
        CancellationToken ct);

    /// <summary>1 つの実行が待機中かを返す。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="execution">対象の実行の投影（認可は呼び出し側で済ませる）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>投影の値が <c>Running</c> で、自身または子孫が待機中に数える Wait を持てば <see langword="true"/>。</returns>
    Task<bool> IsWaitingAsync(ICoreUnitOfWork uow, ExecutionRow execution, CancellationToken ct);
}
