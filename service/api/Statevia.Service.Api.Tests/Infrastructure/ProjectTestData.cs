
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>projects / project_accesses のテストデータ投入。</summary>
internal static class ProjectTestData
{
    /// <summary>所属テナントの既定 project を作成する。</summary>
    public static ProjectRow AddDefaultProject(
        CoreDbContext ctx,
        Guid tenantId,
        string tenantKey,
        Guid? projectId = null)
    {
        var project = new ProjectRow
        {
            ProjectId = projectId ?? Guid.NewGuid(),
            TenantId = tenantId,
            Slug = ProjectRepository.DefaultProjectSlug,
            DisplayName = $"{tenantKey} default",
            CreatedAt = DateTime.UtcNow
        };
        ctx.Projects.Add(project);
        return project;
    }

    /// <summary>テスト用 Principal を追加する。</summary>
    public static void AddPrincipal(CoreDbContext ctx, Guid tenantId, Guid principalId)
    {
        var now = DateTime.UtcNow;
        ctx.Set<PrincipalRow>().Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = tenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.User,
            DisplayName = "principal",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    /// <summary>project_accesses 行を追加する。</summary>
    public static ProjectAccessRow GrantAccess(
        CoreDbContext ctx,
        Guid projectId,
        Guid principalId,
        ProjectAccessRole role)
    {
        var access = new ProjectAccessRow
        {
            ProjectId = projectId,
            PrincipalId = principalId,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        ctx.ProjectAccesses.Add(access);
        return access;
    }
}
