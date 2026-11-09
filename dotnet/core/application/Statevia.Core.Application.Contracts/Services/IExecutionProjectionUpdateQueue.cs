namespace Statevia.Core.Application.Contracts.Services;

/// <summary>
/// ワークフロー投影更新の非同期キュー。
/// </summary>
public interface IExecutionProjectionUpdateQueue
{
    /// <summary>
    /// 指定ワークフローの投影更新をキューへ投入する。
    /// </summary>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>投入が終わると完了するタスク。</returns>
    Task EnqueueAsync(Guid executionId, CancellationToken ct);

    /// <summary>
    /// 指定ワークフローに対する未処理更新が無くなるまで待機する。
    /// </summary>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>未処理の更新が無くなると完了するタスク。</returns>
    Task DrainAsync(Guid executionId, CancellationToken ct);
}
