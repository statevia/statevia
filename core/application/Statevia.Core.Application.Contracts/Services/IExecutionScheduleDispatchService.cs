namespace Statevia.Core.Application.Contracts.Services;

/// <summary>期限到来スケジュールを claim し、既存 Start 経路へ載せる。</summary>
/// <remarks>
/// <para>Runtime の Dispatcher HostedService と、手動 <c>POST /run</c> から呼ばれる。このインターフェース自体は HTTP を持たない。</para>
/// <para>新しい <c>execution_work_items.kind</c> は増やさない。Owner は run-as ServiceAccount。</para>
/// </remarks>
public interface IExecutionScheduleDispatchService
{
    /// <summary>
    /// due かつ enabled の行を最大 <paramref name="limit"/> 件処理する。
    /// </summary>
    /// <param name="utcNow">現在 UTC。</param>
    /// <param name="limit">1 回の最大件数（初版 64）。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>処理した件数（欠発・skip・fail を含む）。</returns>
    Task<int> DispatchDueAsync(DateTime utcNow, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// 手動で 1 回 Start する。<c>next_fire_at</c> は変えない。
    /// </summary>
    /// <param name="scheduleId">対象スケジュール。</param>
    /// <param name="idempotencyKey">手動冪等キーの接尾辞。空なら新規 GUID。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>開始した実行。</returns>
    /// <exception cref="NotFoundException">未削除のスケジュールが無い。</exception>
    /// <exception cref="ApiValidationException">無効、または run-as が使えない。</exception>
    Task<ExecutionResponse> RunManuallyAsync(
        Guid scheduleId,
        string? idempotencyKey,
        CancellationToken cancellationToken);
}
