namespace Statevia.Core.Application.Contracts.Services;

/// <summary>
/// 集合配送: 外部イベントを、topic / key が一致する現在テナントの durable Wait 購読へ配送するユースケース。
/// </summary>
/// <remarks>
/// <para>
/// 照合は <c>execution_wait_subscriptions</c>、再開は永続ワークキューへの Resume 投入で行う。
/// topic 必須、key 任意（省略時は空文字）。配信者は遷移名を指定しない。<c>executions.write</c> を要求する。
/// テナント未解決なら 0 件（他テナントへ積まない）。
/// </para>
/// <para>
/// 照合の対象は、判定する Principal の範囲に入る購読だけである。範囲は、購読の実行の定義の Project に実行者以上で所属し、
/// かつその定義が操作できる定義（Resource Grant）に入ること。判定は Live で、実行へのイベント送信・再開と同じ。
/// テナント管理者も所属が無ければ範囲の外である。並列分岐の物理子の購読は、親と同じ定義で判定される。
/// </para>
/// <para>
/// 範囲の外の購読は黙って除き、呼び出し元には一致 0 件と同じ成功を返す（範囲の外に購読があるかを応答から読み取れないようにする）。
/// 除いた件数は運用者向けのログに残す。
/// </para>
/// </remarks>
public interface IEventIngressService
{
    /// <summary>
    /// HTTP の集合配送。呼び出し元の範囲に入る購読ごとに Resume ワークを投入する。
    /// </summary>
    /// <param name="topic">トピック（必須。前後空白は trim）。</param>
    /// <param name="key">相関キー（未指定・空白は空文字）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>Resume ワークの投入が終わると完了するタスク。</returns>
    /// <exception cref="Security.UnauthorizedException">テナント文脈に Principal が無いとき。</exception>
    Task PublishAsync(
        string topic,
        string key,
        CancellationToken ct);

    /// <summary>
    /// Builtin <c>event.publish</c> の集合配送。発行した実行を開始した Principal の範囲に入る購読ごとに Resume ワークを投入する。
    /// </summary>
    /// <remarks>
    /// 開始者は発行した実行の Snapshot の <c>StartedByPrincipalId</c>（定期実行は run-as、子のワークフローは親の開始者）。
    /// 開始者が無効化・削除されていれば照合せず、警告ログだけを残して成功する。
    /// </remarks>
    /// <param name="publishingExecutionId">発行した実行（<c>StateContext.ExecutionId</c>）。</param>
    /// <param name="topic">トピック（必須。前後空白は trim）。</param>
    /// <param name="key">相関キー（未指定・空白は空文字）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>Resume ワークの投入が終わると完了するタスク。</returns>
    /// <exception cref="InvalidOperationException">発行した実行の行か Snapshot が無いとき（データの不整合）。</exception>
    Task PublishFromExecutionAsync(
        Guid publishingExecutionId,
        string topic,
        string key,
        CancellationToken ct);
}
