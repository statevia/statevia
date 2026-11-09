namespace Statevia.Core.Engine.Scheduler;

/// <summary>
/// 状態実行タスクをスケジュールするインターフェース。
/// 並列数制限などを行い、ワークフローエンジンから状態実行を委譲されます。
/// </summary>
public interface IScheduler
{
    /// <summary>指定した作業をスケジュールして実行し、結果を返します。</summary>
    /// <typeparam name="T">作業の結果の型。</typeparam>
    /// <param name="work">実行する作業。キャンセル トークンを受け取る。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>作業の結果を返すタスク。</returns>
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}
