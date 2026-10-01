using Statevia.Core.Application.Contracts.Persistence;

namespace Statevia.Core.Application.Contracts.Services;

/// <summary>現在テナントのアクティブ購読を、発行候補として列挙する。</summary>
/// <remarks>
/// <para>照合と Resume 投入は <see cref="IEventIngressService"/> に残す。本サービスは列挙のみ。</para>
/// <para><c>executions.read</c> を要求する。応答に displayId / nodeId / payload は含めない。</para>
/// <para>件数上限は 500。topic、key の昇順で先頭だけを返す。</para>
/// </remarks>
public interface IEventSubscriptionQueryService
{
    /// <summary>
    /// 現在テナントの <c>execution_wait_subscriptions</c> から一意な topic / key を返す。
    /// </summary>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>最大 500 件。テナント未解決時は空。</returns>
    Task<IReadOnlyList<EventSubscriptionCandidate>> ListActiveAsync(CancellationToken ct);
}
