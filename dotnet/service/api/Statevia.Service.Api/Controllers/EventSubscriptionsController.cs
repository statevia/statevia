using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Controllers;

/// <summary>現在テナントのアクティブ購読から、集合配送の topic / key 候補を返す API。</summary>
[ApiController]
[Route("v1/event-subscriptions")]
public sealed class EventSubscriptionsController(IEventSubscriptionQueryService subscriptions) : ControllerBase
{
    /// <summary>
    /// 現在テナントの一意な (topic, key) を最大 500 件返す。
    /// </summary>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>候補一覧。他テナントの購読は含まない。</returns>
    /// <remarks>権限は <c>executions.read</c>。Hosted 子実行の購読も含む。</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(EventSubscriptionListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EventSubscriptionListResponse>> List(CancellationToken ct)
    {
        var candidates = await subscriptions.ListActiveAsync(ct).ConfigureAwait(false);
        return Ok(new EventSubscriptionListResponse
        {
            Subscriptions = candidates
                .Select(candidate => new EventSubscriptionCandidateResponse
                {
                    Topic = candidate.Topic,
                    Key = candidate.Key,
                })
                .ToArray(),
        });
    }
}
