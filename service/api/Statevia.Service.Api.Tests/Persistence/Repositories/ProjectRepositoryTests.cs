using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary><see cref="ProjectRepository"/> の検証。</summary>
public sealed class ProjectRepositoryTests
{
    /// <summary>既定 project が無いとき slug=default を作成する。</summary>
    [Fact]
    public async Task EnsureDefaultProjectAsync_WhenMissing_CreatesDefaultProject()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var repo = new ProjectRepository(new DefaultIdGenerator());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        await using (var seed = db.Factory.CreateDbContext())
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = tenantId,
                TenantKey = "owner",
                DisplayName = "Owner",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var project = await repo.EnsureDefaultProjectAsync(uow, tenantId, "owner", default);
        await uow.GetDb().SaveChangesAsync();

        // Assert
        await using var verify = db.Factory.CreateDbContext();
        var stored = await verify.Projects.SingleAsync(p => p.ProjectId == project.ProjectId);
        Assert.Equal(ProjectRepository.DefaultProjectSlug, stored.Slug);
        Assert.Equal(tenantId, stored.TenantId);
    }

    /// <summary>既定 project が既にあるときは既存行を返す。</summary>
    [Fact]
    public async Task EnsureDefaultProjectAsync_WhenExists_ReturnsExistingProject()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var ownerTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repo = new ProjectRepository(new DefaultIdGenerator());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        await using (var seed = db.Factory.CreateDbContext())
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = ownerTenantId,
                TenantKey = "owner",
                DisplayName = "Owner",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            ProjectTestData.AddDefaultProject(seed, ownerTenantId, "owner", projectId);
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var project = await repo.EnsureDefaultProjectAsync(uow, ownerTenantId, "owner", default);

        // Assert
        Assert.Equal(projectId, project.ProjectId);
    }

    /// <summary>所属テナントでもメンバー行が無い Principal は null。</summary>
    [Fact]
    public async Task ResolveEffectiveRoleAsync_SameTenantWithoutMembership_ReturnsNull()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var tenantId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repo = new ProjectRepository(new DefaultIdGenerator());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        SeedTenantProject(db, tenantId, projectId);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var role = await repo.ResolveEffectiveRoleAsync(uow, tenantId, principalId, projectId, default);

        // Assert
        Assert.Null(role);
    }

    /// <summary>メンバー行のロールを返す。</summary>
    [Fact]
    public async Task ResolveEffectiveRoleAsync_Member_ReturnsGrantedRole()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var tenantId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repo = new ProjectRepository(new DefaultIdGenerator());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        SeedTenantProject(db, tenantId, projectId);

        await using (var seed = db.Factory.CreateDbContext())
        {
            ProjectTestData.AddPrincipal(seed, tenantId, principalId);
            ProjectTestData.GrantAccess(seed, projectId, principalId, ProjectAccessRole.Executor);
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var role = await repo.ResolveEffectiveRoleAsync(uow, tenantId, principalId, projectId, default);

        // Assert
        Assert.Equal(ProjectAccessRole.Executor, role);
    }

    /// <summary>他テナント Principal への付与は 422 相当で拒否する。</summary>
    [Fact]
    public async Task GrantAccessAsync_OtherTenantPrincipal_ThrowsApiValidationException()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repo = new ProjectRepository(new DefaultIdGenerator());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        SeedTenantProject(db, tenantId, projectId);

        await using (var seed = db.Factory.CreateDbContext())
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = otherTenantId,
                TenantKey = "other",
                DisplayName = "Other",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            ProjectTestData.AddPrincipal(seed, otherTenantId, principalId);
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var ex = await Assert.ThrowsAsync<ApiValidationException>(() =>
            repo.GrantAccessAsync(uow, projectId, principalId, ProjectAccessRole.Reader, default));

        // Assert
        Assert.Contains("same tenant", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>別テナントの Project は null。</summary>
    [Fact]
    public async Task ResolveEffectiveRoleAsync_OtherTenant_ReturnsNull()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repo = new ProjectRepository(new DefaultIdGenerator());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        SeedTenantProject(db, tenantId, projectId);

        await using (var seed = db.Factory.CreateDbContext())
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = otherTenantId,
                TenantKey = "other",
                DisplayName = "Other",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            ProjectTestData.AddPrincipal(seed, tenantId, principalId);
            ProjectTestData.GrantAccess(seed, projectId, principalId, ProjectAccessRole.Admin);
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var role = await repo.ResolveEffectiveRoleAsync(uow, otherTenantId, principalId, projectId, default);

        // Assert
        Assert.Null(role);
    }

    private static void SeedTenantProject(SqliteTestDatabase db, Guid tenantId, Guid projectId)
    {
        using var seed = db.Factory.CreateDbContext();
        if (!seed.Tenants.Any(t => t.TenantId == tenantId))
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = tenantId,
                TenantKey = "tenant",
                DisplayName = "Tenant",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        ProjectTestData.AddDefaultProject(seed, tenantId, "tenant", projectId);
        seed.SaveChanges();
    }
}
