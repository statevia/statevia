using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Statevia.Core.Application.Services;

/// <summary><see cref="IEventIngressService"/> 実装。</summary>
/// <remarks>
/// <para>入口で <c>executions.write</c> を要求する。照合 SQL に現在 TenantId を明示する。テナント未解決なら enqueue しない。</para>
/// <para>
/// 照合は読み取りトランザクションの 1 つの SQL で、topic / key の一致と、判定する Principal の範囲
/// （購読の実行の定義の Project に実行者以上で所属し、かつその定義が操作できる定義に入る）を同時に求める。
/// 範囲に入る購読だけ Resume ワークを一括 enqueue し、範囲の外は黙って除く。除いた件数だけを
/// <see cref="EventIngressLogMessages.EventIngressSubscriptionsOutOfScope"/> に残す。
/// </para>
/// <para>topic / key は正規化後の厳密一致。配信者は遷移名（event）を指定しない。</para>
/// </remarks>
/// <param name="transactions">読み取りトランザクション実行。</param>
/// <param name="waits">Wait 購読照合。</param>
/// <param name="executions">発行した実行の読み取り（<c>event.publish</c>）。</param>
/// <param name="principals">開始者の有効性の確認（<c>event.publish</c>）。</param>
/// <param name="workQueue">Resume ワーク投入。</param>
/// <param name="idGenerator">ワーク ID 生成。</param>
/// <param name="runtimeAuth">Runtime permission 認可。</param>
/// <param name="tenantContext">現在テナント文脈。</param>
/// <param name="logger">集合配送のログ。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "DI による明示的コンストラクタ注入。")]
internal sealed class EventIngressService(
    ICoreTransactionExecutor transactions,
    IExecutionWaitRepository waits,
    IExecutionRepository executions,
    IPrincipalDataAccess principals,
    IExecutionWorkQueue workQueue,
    IIdGenerator idGenerator,
    IRuntimePermissionAuthorization runtimeAuth,
    ITenantContextAccessor tenantContext,
    ILogger<EventIngressService> logger) : IEventIngressService
{
    /// <summary>HTTP 入口のログ上の名前。</summary>
    private const string HttpOrigin = "http";

    /// <summary><c>event.publish</c> 入口のログ上の名前。</summary>
    private const string ExecutionOrigin = "execution";

    /// <inheritdoc />
    public async Task PublishAsync(
        string topic,
        string key,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        await runtimeAuth
            .EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsWrite, ct)
            .ConfigureAwait(false);

        if (tenantContext.TenantId is not { } tenantId)
            return;

        // Worker から誤って呼ばれたときに、範囲を絞らずに照合しないための確認。HTTP はミドルウェアが先に止める。
        if (tenantContext.PrincipalId is not { } callerPrincipalId)
            throw new UnauthorizedException("Authentication required.", ApiErrorCodes.Unauthorized);

        await DispatchInScopeAsync(tenantId, topic, key, callerPrincipalId, HttpOrigin, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishFromExecutionAsync(
        Guid publishingExecutionId,
        string topic,
        string key,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        await runtimeAuth
            .EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsWrite, ct)
            .ConfigureAwait(false);

        if (tenantContext.TenantId is not { } tenantId)
            return;

        var starterPrincipalId = await ResolveStarterPrincipalIdAsync(tenantId, publishingExecutionId, ct)
            .ConfigureAwait(false);

        var starter = await principals.FindPrincipalAsync(starterPrincipalId, ct).ConfigureAwait(false);
        if (starter is null || !PrincipalIdentityAuthorization.IsLive(starter))
        {
            logger.EventPublishStarterUnavailable(tenantId, publishingExecutionId);
            return;
        }

        await DispatchInScopeAsync(tenantId, topic, key, starterPrincipalId, ExecutionOrigin, ct).ConfigureAwait(false);
    }

    /// <summary>発行した実行の Snapshot から開始者を読む。</summary>
    /// <param name="tenantId">テナント。</param>
    /// <param name="executionId">発行した実行。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>Snapshot の <c>StartedByPrincipalId</c>。</returns>
    /// <exception cref="InvalidOperationException">実行の行か Snapshot が無いとき（データの不整合）。</exception>
    private async Task<Guid> ResolveStarterPrincipalIdAsync(Guid tenantId, Guid executionId, CancellationToken ct)
    {
        var execution = await transactions.ExecuteReadOnlyAsync(
                (uow, innerCt) => executions.GetByIdAsync(uow, tenantId, executionId, innerCt),
                ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Publishing execution was not found.");

        var snapshot = ExecutionSecuritySnapshotJson.TryDeserialize(execution.SecuritySnapshotJson)
            ?? throw new InvalidOperationException("Publishing execution security snapshot is missing.");

        return snapshot.StartedByPrincipalId;
    }

    /// <summary>判定する Principal の範囲に入る購読だけ Resume ワークを積む。</summary>
    /// <param name="tenantId">テナント。</param>
    /// <param name="topic">トピック（未正規化）。</param>
    /// <param name="key">key（未正規化）。</param>
    /// <param name="evaluatedPrincipalId">範囲を判定する Principal。</param>
    /// <param name="origin">ログ上の入口の名前。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>enqueue が終わると完了するタスク。</returns>
    private async Task DispatchInScopeAsync(
        Guid tenantId,
        string topic,
        string key,
        Guid evaluatedPrincipalId,
        string origin,
        CancellationToken ct)
    {
        var normalizedTopic = topic.Trim();
        var normalizedKey = NormalizeKey(key);

        var matches = await transactions.ExecuteReadOnlyAsync(
            (uow, innerCt) => waits.ListMatchingSubscriptionsAsync(
                uow,
                tenantId,
                normalizedTopic,
                normalizedKey,
                evaluatedPrincipalId,
                innerCt),
            ct).ConfigureAwait(false);

        var inScope = matches.Where(match => match.InScope).ToList();
        var outOfScopeCount = matches.Count - inScope.Count;
        if (outOfScopeCount > 0)
            logger.EventIngressSubscriptionsOutOfScope(tenantId, origin, matches.Count, outOfScopeCount);

        if (inScope.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var items = inScope
            .Select(match => new ExecutionWorkItemRow
            {
                WorkItemId = idGenerator.NewSequentialGuid(),
                ExecutionId = match.ExecutionId,
                Kind = ExecutionWorkItemKinds.Resume,
                Payload = JsonSerializer.Serialize(
                    new ExecutionResumeWorkItemPayload(
                        ExecutionResumeWorkItemModes.Event,
                        match.NodeId,
                        match.ResumeEventName),
                    ExecutionWorkItemPayloadJson.Options),
                AvailableAt = now,
                Attempts = 0,
                CreatedAt = now
            })
            .ToList();

        await workQueue.EnqueueManyAsync(items, ct).ConfigureAwait(false);
    }

    /// <summary>key 未指定・空白を空文字へ正規化する。</summary>
    private static string NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
