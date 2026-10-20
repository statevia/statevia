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
    Task GrantAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid principalId,
        ProjectAccessRole role,
        CancellationToken ct);
}
