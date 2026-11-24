namespace Statevia.Core.Application.Services;

/// <summary><see cref="IEventSubscriptionQueryService"/> 実装。</summary>
/// <remarks>
/// <para>入口で <c>executions.read</c> を要求する。列挙 SQL に現在 TenantId を明示する。</para>
/// <para>
/// 候補は、呼び出し元が閲覧できる実行（定義の Project に閲覧者以上で所属し、かつその定義が操作できる定義に入る実行）の購読だけから作る。
/// 実行の一覧と同じ範囲で、テナント管理者も所属が無ければ候補は空になる。範囲に入る Hosted 子実行の購読も含む。
/// Principal が無いときは範囲を絞れないので空を返す。
/// </para>
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

        if (tenantContext.TenantId is not { } tenantId || tenantContext.PrincipalId is not { } principalId)
            return [];

        return await transactions.ExecuteReadOnlyAsync(
            (uow, innerCt) => waits.ListDistinctSubscriptionCandidatesAsync(
                uow,
                tenantId,
                principalId,
                MaxCandidates,
                innerCt),
            ct).ConfigureAwait(false);
    }
}
