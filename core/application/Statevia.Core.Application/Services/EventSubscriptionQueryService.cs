namespace Statevia.Core.Application.Services;

/// <summary><see cref="IEventSubscriptionQueryService"/> 実装。</summary>
/// <remarks>
/// <para>入口で <c>executions.read</c> を要求する。列挙 SQL に現在 TenantId を明示する。</para>
/// <para>Hosted 子実行の購読も同じ表にあるため、子行も候補に含まれる。</para>
/// </remarks>
/// <param name="transactions">読み取りトランザクション実行。</param>
/// <param name="waits">Wait 購読の列挙。</param>
/// <param name="runtimeAuth">Runtime permission 認可。</param>
/// <param name="tenantContext">現在テナント文脈。</param>
internal sealed class EventSubscriptionQueryService(
    ICoreTransactionExecutor transactions,
    IExecutionWaitRepository waits,
    IRuntimePermissionAuthorization runtimeAuth,
    ITenantContextAccessor tenantContext) : IEventSubscriptionQueryService
{
    /// <summary>候補 GET が返す一意な (topic, key) の上限。超過分は昇順の先頭だけを返す。</summary>
    public const int MaxCandidates = 500;

    /// <inheritdoc />
    public async Task<IReadOnlyList<EventSubscriptionCandidate>> ListActiveAsync(CancellationToken ct)
    {
        await runtimeAuth
            .EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsRead, ct)
            .ConfigureAwait(false);

        if (tenantContext.TenantId is not { } tenantId)
            return [];

        return await transactions.ExecuteReadOnlyAsync(
            (uow, innerCt) => waits.ListDistinctSubscriptionCandidatesAsync(
                uow,
                tenantId,
                MaxCandidates,
                innerCt),
            ct).ConfigureAwait(false);
    }
}
