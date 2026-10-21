using Statevia.Core.Application.Contracts.Security;

namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>projects / project_accesses 永続化。</summary>
public interface IProjectRepository
{
    Task<ProjectRow> EnsureDefaultProjectAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        string tenantKey,
        CancellationToken ct);

    /// <summary>当該 Principal のメンバー行だけを実効ロールにする。所属テナントが違う、またはメンバー行が無いときは null。</summary>
    Task<ProjectAccessRole?> ResolveEffectiveRoleAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Project が指定テナントに所属するか。</summary>
    Task<bool> ExistsInTenantAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>同一テナントの Principal に Project ロールを付与する。</summary>
    /// <exception cref="ApiValidationException">Principal と Project のテナントが一致しないとき。</exception>
    Task GrantAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid principalId,
        ProjectAccessRole role,
        CancellationToken ct);

    /// <summary>呼び出し元 Principal がメンバーの Project と実効ロールを一覧する。</summary>
    Task<IReadOnlyList<(ProjectRow Project, ProjectAccessRole Role)>> ListMembershipAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        CancellationToken ct);

    /// <summary>テナント内の Project を取得する（追跡しない）。</summary>
    Task<ProjectRow?> GetInTenantAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>テナント内の Project を更新用に追跡取得する。</summary>
    Task<ProjectRow?> GetInTenantForUpdateAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Project 行を追加する。</summary>
    Task AddAsync(ICoreUnitOfWork uow, ProjectRow project, CancellationToken ct);

    /// <summary>テナント内で slug が既に使われているか。</summary>
    Task<bool> ExistsSlugInTenantAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        string slug,
        Guid? excludingProjectId,
        CancellationToken ct);

    /// <summary>テナント内の Project を slug 順で返す（メンバーシップでは絞らない）。</summary>
    Task<IReadOnlyList<ProjectRow>> ListInTenantAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        CancellationToken ct);

    /// <summary>Project のメンバー行を作成日時順で返す。</summary>
    Task<IReadOnlyList<ProjectAccessRow>> ListMembersAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        CancellationToken ct);

    /// <summary>メンバー行を更新用に追跡取得する。無いときは null。</summary>
    Task<ProjectAccessRow?> GetAccessForUpdateAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid principalId,
        CancellationToken ct);

    /// <summary>指定ロールのメンバー数。</summary>
    Task<int> CountByRoleAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        ProjectAccessRole role,
        CancellationToken ct);

    /// <summary>メンバー行が無ければ付与し、あればロールを上書きする。</summary>
    /// <exception cref="ApiValidationException">Principal と Project のテナントが一致しないとき。</exception>
    Task UpsertAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid principalId,
        ProjectAccessRole role,
        CancellationToken ct);

    /// <summary>メンバー行を削除する。行が無ければ false。</summary>
    Task<bool> RemoveAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid principalId,
        CancellationToken ct);
}
