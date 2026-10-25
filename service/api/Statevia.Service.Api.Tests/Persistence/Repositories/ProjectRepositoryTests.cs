using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary><see cref="ProjectRepository"/> の検証。</summary>
public sealed class ProjectRepositoryTests
{
    /// <summary>既定 project が無いとき作成し、作成者だけを admin にする。</summary>
    [Fact]
    public async Task EnsureDefaultProjectAsync_WhenMissing_CreatesDefaultProject()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var repo = new ProjectRepository(new DefaultIdGenerator());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        var creatorId = Guid.NewGuid();
        var bystanderId = Guid.NewGuid();
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
            ProjectTestData.AddPrincipal(seed, tenantId, creatorId);
            ProjectTestData.AddPrincipal(seed, tenantId, bystanderId);
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var project = await repo.EnsureDefaultProjectAsync(uow, tenantId, "owner", creatorId, default);
        await uow.GetDb().SaveChangesAsync();

        // Assert
        await using var verify = db.Factory.CreateDbContext();
        var stored = await verify.Projects.SingleAsync(p => p.ProjectId == project.ProjectId);
        Assert.Equal(ProjectRepository.DefaultProjectSlug, stored.Slug);
        Assert.Equal(tenantId, stored.TenantId);
        var admin = await verify.ProjectAccesses.SingleAsync(a => a.ProjectId == project.ProjectId);
        Assert.Equal(creatorId, admin.PrincipalId);
        Assert.Equal(ProjectAccessRole.Admin, admin.Role);
    }

    /// <summary>既定 project が既にあるときは既存行を返し、呼び出し元をメンバーにしない。</summary>
    [Fact]
    public async Task EnsureDefaultProjectAsync_WhenExists_ReturnsExistingProject()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var ownerTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repo = new ProjectRepository(new DefaultIdGenerator());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        var callerId = Guid.NewGuid();
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
            ProjectTestData.AddPrincipal(seed, ownerTenantId, callerId);
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var project = await repo.EnsureDefaultProjectAsync(uow, ownerTenantId, "owner", callerId, default);
        await uow.GetDb().SaveChangesAsync();

        // Assert
        Assert.Equal(projectId, project.ProjectId);
        await using var verify = db.Factory.CreateDbContext();
        Assert.Empty(await verify.ProjectAccesses.Where(a => a.ProjectId == projectId).ToListAsync());
    }

    /// <summary>既存 default へユーザーと API キーだけを admin で一度入れる。再実行は増えない。</summary>
    [Fact]
    public async Task BackfillDefaultProjectAdminsAsync_InsertsUsersAndApiKeysOnce()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var tenantId = Guid.NewGuid();
        var defaultProjectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var alreadyReaderId = Guid.NewGuid();
        var apiKeyPrincipalId = Guid.NewGuid();
        var serviceAccountId = Guid.NewGuid();
        var systemId = Guid.NewGuid();
        var deletedUserId = Guid.NewGuid();
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
            ProjectTestData.AddDefaultProject(seed, tenantId, "owner", defaultProjectId);
            seed.Projects.Add(new ProjectRow
            {
                ProjectId = otherProjectId,
                TenantId = tenantId,
                Slug = "other",
                DisplayName = "other",
                CreatedAt = now
            });
            AddTypedPrincipal(seed, tenantId, userId, PrincipalType.User, now);
            AddTypedPrincipal(seed, tenantId, alreadyReaderId, PrincipalType.User, now);
            AddTypedPrincipal(seed, tenantId, apiKeyPrincipalId, PrincipalType.ServiceAccount, now);
            AddTypedPrincipal(seed, tenantId, serviceAccountId, PrincipalType.ServiceAccount, now);
            AddTypedPrincipal(seed, tenantId, systemId, PrincipalType.System, now);
            AddTypedPrincipal(seed, tenantId, deletedUserId, PrincipalType.User, now, deletedAt: now);
            ProjectTestData.GrantAccess(seed, defaultProjectId, alreadyReaderId, ProjectAccessRole.Reader);
            seed.ApiKeys.Add(new ApiKeyRow
            {
                ApiKeyId = Guid.NewGuid(),
                TenantId = tenantId,
                PrincipalId = apiKeyPrincipalId,
                KeyPrefix = "sv_test",
                KeyHash = "hash",
                AllowedScopesJson = "[]",
                CreatedAt = now
            });
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var inserted = await repo.BackfillDefaultProjectAdminsAsync(uow, default);
        var insertedAgain = await repo.BackfillDefaultProjectAdminsAsync(uow, default);
        await uow.GetDb().SaveChangesAsync();

        // Assert
        Assert.Equal(2, inserted);
        Assert.Equal(0, insertedAgain);
        await using var verify = db.Factory.CreateDbContext();
        var defaultRoles = await verify.ProjectAccesses
            .Where(a => a.ProjectId == defaultProjectId)
            .ToDictionaryAsync(a => a.PrincipalId, a => a.Role);
        Assert.Equal(ProjectAccessRole.Admin, defaultRoles[userId]);
        Assert.Equal(ProjectAccessRole.Admin, defaultRoles[apiKeyPrincipalId]);
        Assert.Equal(ProjectAccessRole.Reader, defaultRoles[alreadyReaderId]);
        Assert.DoesNotContain(serviceAccountId, defaultRoles.Keys);
        Assert.DoesNotContain(systemId, defaultRoles.Keys);
        Assert.DoesNotContain(deletedUserId, defaultRoles.Keys);
        Assert.Empty(await verify.ProjectAccesses.Where(a => a.ProjectId == otherProjectId).ToListAsync());
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

    private static void AddTypedPrincipal(
        CoreDbContext ctx,
        Guid tenantId,
        Guid principalId,
        PrincipalType principalType,
        DateTime now,
        DateTime? deletedAt = null)
    {
        ctx.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = tenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = principalType,
            DisplayName = principalType.ToString(),
            IsActive = deletedAt is null,
            DeletedAt = deletedAt,
            CreatedAt = now,
            UpdatedAt = now
        });
    }
}
