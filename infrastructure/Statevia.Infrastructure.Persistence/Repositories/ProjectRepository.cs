using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Contracts;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary><see cref="IProjectRepository"/> の EF 実装。</summary>
internal sealed class ProjectRepository(IIdGenerator idGenerator) : IProjectRepository
{
    /// <summary>移行・初回定義作成時の既定 project slug。</summary>
    public const string DefaultProjectSlug = "default";

    /// <inheritdoc />
    public async Task<ProjectRow> EnsureDefaultProjectAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        string tenantKey,
        CancellationToken ct)
    {
        var existing = await uow.GetDb().Projects
            .FirstOrDefaultAsync(
                p => p.TenantId == tenantId && p.Slug == DefaultProjectSlug,
                ct)
            .ConfigureAwait(false);

        if (existing is not null)
            return existing;

        var now = DateTime.UtcNow;
        var project = new ProjectRow
        {
            ProjectId = idGenerator.NewSequentialGuid(),
            TenantId = tenantId,
            Slug = DefaultProjectSlug,
            DisplayName = $"{tenantKey} default",
            Description = "Auto-created default project",
            CreatedAt = now
        };
        uow.GetDb().Projects.Add(project);
        return project;
    }

    /// <inheritdoc />
    public async Task<ProjectAccessRole?> ResolveEffectiveRoleAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        Guid projectId,
        CancellationToken ct)
    {
        var project = await uow.GetDb().Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == projectId, ct)
            .ConfigureAwait(false);

        if (project is null || project.TenantId != tenantId)
            return null;

        var access = await uow.GetDb().ProjectAccesses.AsNoTracking()
            .FirstOrDefaultAsync(
                pa => pa.ProjectId == projectId && pa.PrincipalId == principalId,
                ct)
            .ConfigureAwait(false);

        return access?.Role;
    }

    /// <inheritdoc />
    public Task<bool> ExistsInTenantAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        uow.GetDb().Projects.AsNoTracking()
            .AnyAsync(p => p.ProjectId == projectId && p.TenantId == tenantId, ct);

    /// <inheritdoc />
    /// <exception cref="ApiValidationException">Principal と Project のテナントが一致しないとき。</exception>
    public async Task GrantAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid principalId,
        ProjectAccessRole role,
        CancellationToken ct)
    {
        var db = uow.GetDb();
        // 同一 UoW で Add 直後の行も見えるように Local を先に見る。
        var projectTenantId = db.Projects.Local
            .Where(p => p.ProjectId == projectId)
            .Select(p => (Guid?)p.TenantId)
            .FirstOrDefault();
        if (projectTenantId is null)
        {
            projectTenantId = await db.Projects.AsNoTracking()
                .Where(p => p.ProjectId == projectId)
                .Select(p => (Guid?)p.TenantId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        if (projectTenantId is null)
        {
            throw new ApiValidationException(
                "Project was not found.",
                new[] { new { message = "Project was not found.", field = "projectId" } });
        }

        var principalTenantId = db.Principals.Local
            .Where(p => p.PrincipalId == principalId)
            .Select(p => (Guid?)p.TenantId)
            .FirstOrDefault();
        if (principalTenantId is null)
        {
            principalTenantId = await db.Principals.AsNoTracking()
                .Where(p => p.PrincipalId == principalId)
                .Select(p => (Guid?)p.TenantId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }
        if (principalTenantId is null || principalTenantId.Value != projectTenantId.Value)
        {
            throw new ApiValidationException(
                "Principal must belong to the same tenant as the project.",
                new[] { new { message = "Principal must belong to the same tenant as the project.", field = "principalId" } });
        }

        db.ProjectAccesses.Add(new ProjectAccessRow
        {
            ProjectId = projectId,
            PrincipalId = principalId,
            Role = role,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(ProjectRow Project, ProjectAccessRole Role)>> ListMembershipAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        CancellationToken ct)
    {
        var rows = await (
                from access in uow.GetDb().ProjectAccesses.AsNoTracking()
                join project in uow.GetDb().Projects.AsNoTracking()
                    on access.ProjectId equals project.ProjectId
                where access.PrincipalId == principalId && project.TenantId == tenantId
                orderby project.Slug
                select new { project, access.Role })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows.ConvertAll(row => (row.project, row.Role));
    }

    /// <inheritdoc />
    public Task<ProjectRow?> GetInTenantAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        uow.GetDb().Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.TenantId == tenantId, ct);

    /// <inheritdoc />
    public Task<ProjectRow?> GetInTenantForUpdateAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        uow.GetDb().Projects
            .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.TenantId == tenantId, ct);

    /// <inheritdoc />
    public Task AddAsync(ICoreUnitOfWork uow, ProjectRow project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        uow.GetDb().Projects.Add(project);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsSlugInTenantAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        string slug,
        Guid? excludingProjectId,
        CancellationToken ct)
    {
        var query = uow.GetDb().Projects.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Slug == slug);
        if (excludingProjectId is { } excludeId)
            query = query.Where(p => p.ProjectId != excludeId);

        return query.AnyAsync(ct);
    }
}
