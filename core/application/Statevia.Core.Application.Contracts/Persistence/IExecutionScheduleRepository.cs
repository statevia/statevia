namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary><c>schedules</c> / <c>schedule_runs</c> の永続化。</summary>
/// <remarks>
/// Dispatcher の due claim は QueryFilter を外し全テナントを見る。
/// CRUD は呼び出し側テナント文脈に従う。
/// </remarks>
public interface IExecutionScheduleRepository
{
    /// <summary>テナント内の未削除スケジュールを 1 件取得する。</summary>
    Task<ExecutionScheduleRow?> GetByIdAsync(Guid scheduleId, CancellationToken cancellationToken);

    /// <summary>テナント内の未削除スケジュールを一覧する。</summary>
    Task<IReadOnlyList<ExecutionScheduleRow>> ListAsync(CancellationToken cancellationToken);

    /// <summary>スケジュールを追加する。</summary>
    Task AddAsync(ExecutionScheduleRow row, CancellationToken cancellationToken);

    /// <summary>既存行を保存する。</summary>
    Task UpdateAsync(ExecutionScheduleRow row, CancellationToken cancellationToken);

    /// <summary>枠結果を追加する。同一枠の二重 INSERT は一意制約で失敗する。</summary>
    Task AddRunAsync(ExecutionScheduleRunRow row, CancellationToken cancellationToken);

    /// <summary>
    /// due かつ enabled の行を排他取得する（PostgreSQL は <c>FOR UPDATE SKIP LOCKED</c>）。
    /// </summary>
    /// <param name="uow">開始済みトランザクションを持つ UoW。コミットは呼び出し側。</param>
    /// <param name="utcNow">現在 UTC。</param>
    /// <param name="limit">最大件数。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>ロックした due 行。</returns>
    Task<IReadOnlyList<ExecutionScheduleRow>> ClaimDueAsync(
        ICoreUnitOfWork uow,
        DateTime utcNow,
        int limit,
        CancellationToken cancellationToken);
}
