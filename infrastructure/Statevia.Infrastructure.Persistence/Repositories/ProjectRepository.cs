using Microsoft.EntityFrameworkCore;

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
    public Task GrantAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid principalId,
        ProjectAccessRole role,
        CancellationToken ct)
    {
        uow.GetDb().ProjectAccesses.Add(new ProjectAccessRow
        {
            ProjectId = projectId,
            PrincipalId = principalId,
            Role = role,
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }
}
