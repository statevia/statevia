using Microsoft.EntityFrameworkCore;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>
/// 待機中の実行を求める読み取りの部品。一覧の絞り込み・一覧の <c>waiting</c>・件数・定義の最後の実行で共通に使う。
/// </summary>
/// <remarks>
/// <para>
/// 待機中とは、自身または子孫の物理子 Execution が、人や外部の入力を待つ Wait（<c>EventWait</c> / <c>CallbackWait</c>）を
/// <c>execution_waits</c> に 1 つ以上持ち、投影の値が <c>Running</c> の実行をいう。
/// 期限で自動に再開する <c>DelayWait</c> は数えない。
/// </para>
/// <para>
/// 子孫の待機は、Wait を持つ実行から <c>execution_branches</c> を親へ上ってルートまで集める。
/// 再帰 CTE の代わりに Fork の深さの回数だけクエリを繰り返す。パラメータ化した LINQ だけで書けて、InMemory のテストでも同じ判定を確かめられるため。
/// 集合の大きさは待機している実行の数と Fork の深さで決まり、実行の総数には比例しない（<c>execution_waits</c> は終端時に消える）。
/// </para>
/// <para><c>execution_waits</c> は投影なので、Worker の投影が遅れる間はグラフのスナップショットより遅れることがある。</para>
/// </remarks>
internal static class ExecutionWaitingQueries
{
    /// <summary>待機中に数える Wait の種類。</summary>
    private static readonly ExecutionWaitKind[] AttentionWaitKinds =
    [
        ExecutionWaitKind.EventWait,
        ExecutionWaitKind.CallbackWait
    ];

    /// <summary>テナント内で、待機中に数える Wait を持つ実行と、その祖先の実行の ID を集める。</summary>
    /// <param name="db">読み取りコンテキスト。</param>
    /// <param name="tenantId">対象テナント。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>実行 ID の配列（順不同）。ルートの実行が含まれれば、そのルートは投影の値が <c>Running</c> のとき待機中である。</returns>
    public static async Task<Guid[]> LoadWaitingExecutionIdsAsync(CoreDbContext db, Guid tenantId, CancellationToken ct)
    {
        var tenantExecutionIds = db.Executions.AsNoTracking()
            .Where(execution => execution.TenantId == tenantId)
            .Select(execution => execution.ExecutionId);
        var waitingIds = await db.ExecutionWaits.AsNoTracking()
            .Where(wait => AttentionWaitKinds.Contains(wait.WaitKind) && tenantExecutionIds.Contains(wait.ExecutionId))
            .Select(wait => wait.ExecutionId)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var collected = waitingIds.ToHashSet();
        var frontier = waitingIds;
        while (frontier.Count > 0)
        {
            var parentIds = await db.ExecutionBranches.AsNoTracking()
                .Where(branch => frontier.Contains(branch.ExecutionId))
                .Select(branch => branch.ParentExecutionId)
                .Distinct()
                .ToListAsync(ct)
                .ConfigureAwait(false);
            frontier = parentIds.Where(parentId => !collected.Contains(parentId)).ToList();
            collected.UnionWith(frontier);
        }

        return [.. collected];
    }

    /// <summary>1 つの実行について、自身または子孫が待機中に数える Wait を持つかを返す。</summary>
    /// <param name="db">読み取りコンテキスト。</param>
    /// <param name="executionId">対象の実行。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>Wait があれば <see langword="true"/>。投影の値（<c>Running</c> か）は呼び出し側で確かめる。</returns>
    public static async Task<bool> HasAttentionWaitInTreeAsync(CoreDbContext db, Guid executionId, CancellationToken ct)
    {
        var collected = new HashSet<Guid> { executionId };
        List<Guid> frontier = [executionId];
        while (frontier.Count > 0)
        {
            var childIds = await db.ExecutionBranches.AsNoTracking()
                .Where(branch => frontier.Contains(branch.ParentExecutionId))
                .Select(branch => branch.ExecutionId)
                .Distinct()
                .ToListAsync(ct)
                .ConfigureAwait(false);
            frontier = childIds.Where(childId => !collected.Contains(childId)).ToList();
            collected.UnionWith(frontier);
        }

        var treeIds = collected.ToArray();
        return await db.ExecutionWaits.AsNoTracking()
            .AnyAsync(wait => AttentionWaitKinds.Contains(wait.WaitKind) && treeIds.Contains(wait.ExecutionId), ct)
            .ConfigureAwait(false);
    }
}
