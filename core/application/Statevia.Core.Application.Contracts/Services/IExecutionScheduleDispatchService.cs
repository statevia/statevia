namespace Statevia.Core.Application.Contracts.Services;

/// <summary>期限到来スケジュールを claim し、既存 Start 経路へ載せる。</summary>
/// <remarks>
/// <para>Runtime の Dispatcher HostedService から呼ばれる。HTTP は公開しない。</para>
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
}
