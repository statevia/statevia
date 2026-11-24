using Microsoft.EntityFrameworkCore;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>
/// Principal が実行を扱える定義の範囲を、定義 ID のサブクエリとして組み立てる。
/// </summary>
/// <remarks>
/// <para>
/// 範囲は次の AND である。定義がテナントに属する。定義の Project に、Principal が指定ロール以上で所属する
/// （<see cref="ProjectAccessQueries"/>。テナント管理者も特別扱いしない）。定義が Principal の操作できる定義（Resource Grant）に入る
/// （<see cref="ResourceGrantListScope"/>。User と ServiceAccount だけを制限し、行が無い種別は制限しない）。
/// </para>
/// <para>
/// 実行の一覧・件数（閲覧者以上）、集合配送の照合（実行者以上）、集合配送の候補の一覧（閲覧者以上）が同じ範囲を使う。
/// 範囲の解釈をここ 1 か所にし、入口ごとに食い違わないようにする。論理削除された定義も範囲に含める（既存の実行を扱うため）。
/// </para>
/// <para>
/// 並列分岐の物理子の実行は親と同じ <c>definition_id</c> を持つので、子の実行もルートの実行の定義で判定される。
/// </para>
/// </remarks>
internal static class ExecutionDefinitionAccessScope
{
    /// <summary>範囲に入る定義 ID のサブクエリを返す。</summary>
    /// <param name="db">読み取りコンテキスト。</param>
    /// <param name="tenantId">対象テナント。</param>
    /// <param name="principalId">判定する Principal。</param>
    /// <param name="minimumRole">Project に求める最小のロール。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>まだ実行していない定義 ID のクエリ。操作できる定義の行だけはこの呼び出しで 1 回読む。</returns>
    public static async Task<IQueryable<Guid>> BuildAsync(
        CoreDbContext db,
        Guid tenantId,
        Guid principalId,
        ProjectAccessRole minimumRole,
        CancellationToken ct)
    {
        var accessibleProjectIds = ProjectAccessQueries.AccessibleProjectIds(db, principalId, minimumRole);
        var definitions = db.Definitions.AsNoTracking()
            .Where(definition => definition.TenantId == tenantId && accessibleProjectIds.Contains(definition.ProjectId));

        var grantScope = await ResourceGrantListScope.LoadAsync(db, principalId, ct).ConfigureAwait(false);
        if (grantScope.ProjectIds is { } grantedProjectIds)
            definitions = definitions.Where(definition => grantedProjectIds.Contains(definition.ProjectId));

        if (grantScope.DefinitionIds is { } grantedDefinitionIds)
            definitions = definitions.Where(definition => grantedDefinitionIds.Contains(definition.DefinitionId));

        return definitions.Select(definition => definition.DefinitionId);
    }
}
