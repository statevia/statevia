namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary><c>schedules</c> / <c>schedule_runs</c> の永続化。</summary>
/// <remarks>
/// Dispatcher の due claim は QueryFilter を外し全テナントを見る。
/// CRUD は呼び出し側テナント文脈に従う。
/// </remarks>
public interface IExecutionScheduleRepository
{
    /// <summary>テナントから見える未削除スケジュールを 1 件取得する。システム行は不在と同じ。</summary>
    Task<ExecutionScheduleRow?> GetByIdAsync(Guid scheduleId, CancellationToken cancellationToken);

    /// <summary>テナントから見える未削除スケジュールを一覧する。</summary>
    Task<IReadOnlyList<ExecutionScheduleRow>> ListAsync(CancellationToken cancellationToken);

    /// <summary>未削除のシステム行があるか。テナントフィルタは使わない。</summary>
    /// <param name="tenantId">対象テナント。</param>
    /// <param name="jobKey">ジョブ識別子。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    Task<bool> SystemJobExistsAsync(Guid tenantId, string jobKey, CancellationToken cancellationToken);

    /// <summary>テナント内の未削除スケジュールを名前で取得する。</summary>
    Task<ExecutionScheduleRow?> GetByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken);

    /// <summary>スケジュールを追加する。</summary>
    Task AddAsync(ExecutionScheduleRow row, CancellationToken cancellationToken);

    /// <summary>既存行を保存する。</summary>
    Task UpdateAsync(ExecutionScheduleRow row, CancellationToken cancellationToken);

    /// <summary>枠結果を追加する。同一枠の二重 INSERT は一意制約で失敗する。</summary>
    Task AddRunAsync(ExecutionScheduleRunRow row, CancellationToken cancellationToken);

    /// <summary>枠結果を UoW に追加する。SaveChanges は呼び出し側。</summary>
    Task AddRunAsync(ICoreUnitOfWork uow, ExecutionScheduleRunRow row, CancellationToken cancellationToken);

    /// <summary>同一スケジュール由来の非終端実行があるか。</summary>
    Task<bool> HasNonTerminalStartedRunAsync(
        ICoreUnitOfWork uow,
        Guid scheduleId,
        CancellationToken cancellationToken);

    /// <summary><c>next_fire_at</c> を進める。ExecuteUpdate のため即時反映する。</summary>
    Task AdvanceNextFireAtAsync(
        ICoreUnitOfWork uow,
        Guid scheduleId,
        DateTime nextFireAtUtc,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken);

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

    /// <summary>
    /// 待機が無く、投影が <paramref name="updatedAtOrBefore"/> 以前の Running 件数。
    /// </summary>
    /// <param name="uow">読み取りに使う UoW。</param>
    /// <param name="tenantId">点検中スケジュールのテナント。他テナントは数えない。</param>
    /// <param name="updatedAtOrBefore">この時刻以前の <c>updated_at</c> を停滞とする。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    Task<int> CountStuckExecutionsAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        DateTime updatedAtOrBefore,
        CancellationToken cancellationToken);

    /// <summary>テナントスケジュールの failed run で、<paramref name="createdAfter"/> より後の件数。</summary>
    /// <param name="uow">読み取りに使う UoW。</param>
    /// <param name="tenantId">点検中スケジュールのテナント。</param>
    /// <param name="createdAfter">この時刻より後の <c>created_at</c>。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    Task<int> CountFailedTenantScheduleRunsAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        DateTime createdAfter,
        CancellationToken cancellationToken);

    /// <summary>当該システム行の直近 <c>completed</c> の <c>scheduled_fire_at</c>。無ければ null。</summary>
    /// <param name="uow">読み取りに使う UoW。</param>
    /// <param name="scheduleId">点検中のシステムスケジュール。</param>
    /// <param name="tenantId">点検中スケジュールのテナント。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    Task<DateTime?> FindLatestCompletedFireAtAsync(
        ICoreUnitOfWork uow,
        Guid scheduleId,
        Guid tenantId,
        CancellationToken cancellationToken);
}
