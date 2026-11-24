using Statevia.Core.Application.Contracts.Persistence;

namespace Statevia.Core.Application.Contracts.Services;

/// <summary>呼び出し元が閲覧できる実行のアクティブ購読を、集合配送の候補として列挙する。</summary>
/// <remarks>
/// <para>照合と Resume 投入は <see cref="IEventIngressService"/> に残す。本サービスは列挙のみ。</para>
/// <para><c>executions.read</c> を要求する。応答に displayId / nodeId / payload は含めない。</para>
/// <para>件数上限は 500。topic、key の昇順で先頭だけを返す。</para>
/// <para>
/// 閲覧できる実行は、定義の Project に閲覧者以上で所属し、かつその定義が操作できる定義に入る実行（実行の一覧と同じ範囲）。
/// 範囲で絞ってから上限を掛ける。
/// </para>
/// </remarks>
public interface IEventSubscriptionQueryService
{
    /// <summary>
    /// 呼び出し元が閲覧できる実行の <c>execution_wait_subscriptions</c> から一意な topic / key を返す。
    /// </summary>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>最大 500 件。テナント未解決時と Principal が無いときは空。</returns>
    Task<IReadOnlyList<EventSubscriptionCandidate>> ListActiveAsync(CancellationToken ct);
}
