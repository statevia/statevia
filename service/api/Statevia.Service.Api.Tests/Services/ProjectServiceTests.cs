using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ProjectService"/> の Project HTTP 契約（一覧・取得・作成・PATCH）。</summary>
public sealed class ProjectServiceTests
{
    private static readonly Guid CreatorPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OtherPrincipalId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    /// <summary>未所属の GET は 404。</summary>
    [Fact]
    public async Task GetAsync_NonMember_ThrowsNotFound()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        var sut = CreateSut(db, OtherPrincipalId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetAsync(projectId, CancellationToken.None));
    }

    /// <summary>メンバーなら GET でロール付き応答を返す。</summary>
    [Fact]
    public async Task GetAsync_Member_ReturnsRole()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        var sut = CreateSut(db, CreatorPrincipalId);

        // Act
        var response = await sut.GetAsync(projectId, CancellationToken.None);

        // Assert
        Assert.Equal(projectId, response.ProjectId);
        Assert.Equal("admin", response.Role);
        Assert.Equal("default", response.Slug);
    }

    /// <summary>一覧は所属 Project のみ。</summary>
    [Fact]
    public async Task ListAsync_ReturnsOnlyMembershipProjects()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        await using (var seed = new CoreDbContext(db.Options))
        {
            seed.Projects.Add(new ProjectRow
            {
                ProjectId = Guid.NewGuid(),
                TenantId = TestTenantIds.DefaultTenantId,
                Slug = "other",
                DisplayName = "Other",
                CreatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var sut = CreateSut(db, CreatorPrincipalId);

        // Act
        var list = await sut.ListAsync(CancellationToken.None);

        // Assert
        var item = Assert.Single(list);
        Assert.Equal(projectId, item.ProjectId);
    }

    /// <summary><c>projects.create</c> が無い作成は 403。</summary>
    [Fact]
    public async Task CreateAsync_WithoutProjectsCreate_ThrowsForbidden()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        await SeedTenantWithProjectAsync(db, grantCreatorAdmin: false);
        var sut = CreateSut(db, CreatorPrincipalId, denyProjectsCreate: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.CreateAsync(
                new CreateProjectRequest { Slug = "new-proj", DisplayName = "New" },
                CancellationToken.None));
        Assert.Equal("PERMISSION_DENIED", ex.Code);
    }

    /// <summary>作成者だけが初期 admin。同一 tx でメンバー行が入る。</summary>
    [Fact]
    public async Task CreateAsync_GrantsCreatorAdminOnly()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        await SeedTenantWithProjectAsync(db, grantCreatorAdmin: false);
        await using (var seed = new CoreDbContext(db.Options))
        {
            ProjectTestData.AddPrincipal(seed, TestTenantIds.DefaultTenantId, OtherPrincipalId);
            await seed.SaveChangesAsync();
        }

        var sut = CreateSut(db, CreatorPrincipalId);

        // Act
        var created = await sut.CreateAsync(
            new CreateProjectRequest
            {
                Slug = "alpha",
                DisplayName = "Alpha",
                Description = "desc"
            },
            CancellationToken.None);

        // Assert
        Assert.Equal("admin", created.Role);
        Assert.Equal("alpha", created.Slug);
        Assert.Equal("desc", created.Description);

        await using var verify = new CoreDbContext(db.Options);
        var accesses = await verify.ProjectAccesses
            .Where(a => a.ProjectId == created.ProjectId)
            .ToListAsync();
        var access = Assert.Single(accesses);
        Assert.Equal(CreatorPrincipalId, access.PrincipalId);
        Assert.Equal(ProjectAccessRole.Admin, access.Role);
    }

    /// <summary>slug の PATCH が通る。</summary>
    [Fact]
    public async Task UpdateAsync_Admin_UpdatesSlug()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        var sut = CreateSut(db, CreatorPrincipalId);

        // Act
        var updated = await sut.UpdateAsync(
            projectId,
            new UpdateProjectRequest { Slug = "renamed" },
            CancellationToken.None);

        // Assert
        Assert.Equal("renamed", updated.Slug);
        await using var verify = new CoreDbContext(db.Options);
        var stored = await verify.Projects.SingleAsync(p => p.ProjectId == projectId);
        Assert.Equal("renamed", stored.Slug);
    }

    /// <summary>admin 未満の PATCH は 403。</summary>
    [Fact]
    public async Task UpdateAsync_Reader_ThrowsForbidden()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: false);
        await using (var seed = new CoreDbContext(db.Options))
        {
            ProjectTestData.AddPrincipal(seed, TestTenantIds.DefaultTenantId, OtherPrincipalId);
            ProjectTestData.GrantAccess(seed, projectId, OtherPrincipalId, ProjectAccessRole.Reader);
            await seed.SaveChangesAsync();
        }

        var sut = CreateSut(db, OtherPrincipalId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UpdateAsync(
                projectId,
                new UpdateProjectRequest { DisplayName = "Nope" },
                CancellationToken.None));
        Assert.Equal("PROJECT_ACCESS_DENIED", ex.Code);
    }

    /// <summary>他テナントの Project は見えない。</summary>
    [Fact]
    public async Task GetAsync_OtherTenant_ThrowsNotFound()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        var otherTenant = new FixedTenantContextAccessor(new TenantContextState(
            TestTenantIds.OtherTenantId,
            "other",
            CreatorPrincipalId,
            TenantLifecycle.Active));
        var sut = CreateSut(db, CreatorPrincipalId, tenant: otherTenant);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetAsync(projectId, CancellationToken.None));
    }

    /// <summary>重複 slug の作成は 422。</summary>
    [Fact]
    public async Task CreateAsync_DuplicateSlug_ThrowsApiValidation()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        await SeedTenantWithProjectAsync(db, grantCreatorAdmin: false);
        var sut = CreateSut(db, CreatorPrincipalId);

        // Act & Assert
        await Assert.ThrowsAsync<ApiValidationException>(() =>
            sut.CreateAsync(
                new CreateProjectRequest { Slug = "default", DisplayName = "Dup" },
                CancellationToken.None));
    }

    private static ProjectService CreateSut(
        InMemoryTestDatabase db,
        Guid principalId,
        bool denyProjectsCreate = false,
        ITenantContextAccessor? tenant = null)
    {
        var tenantAccessor = tenant ?? new FixedTenantContextAccessor(
            TestTenantIds.DefaultContext with { PrincipalId = principalId });
        var projectRepo = new ProjectRepository(new DefaultIdGenerator());
        var executor = new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory));
        IRuntimePermissionAuthorization runtimeAuth = denyProjectsCreate
            ? new DenyingRuntimePermissionAuthorization()
            : new AllowAllRuntimePermissionAuthorization();

        return new ProjectService(
            projectRepo,
            new ProjectAuthorizationService(projectRepo, tenantAccessor, new DenyTenantAdminAuthorization()),
            tenantAccessor,
            runtimeAuth,
            new DefaultIdGenerator(),
            executor);
    }

    private static async Task<Guid> SeedTenantWithProjectAsync(InMemoryTestDatabase db, bool grantCreatorAdmin)
    {
        var now = DateTime.UtcNow;
        await using var seed = new CoreDbContext(db.Options);
        seed.Tenants.Add(new TenantRow
        {
            TenantId = TestTenantIds.DefaultTenantId,
            TenantKey = "default",
            DisplayName = "Default",
            Lifecycle = TenantLifecycle.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        var project = ProjectTestData.AddDefaultProject(seed, TestTenantIds.DefaultTenantId, "default");
        ProjectTestData.AddPrincipal(seed, TestTenantIds.DefaultTenantId, CreatorPrincipalId);
        if (grantCreatorAdmin)
            ProjectTestData.GrantAccess(seed, project.ProjectId, CreatorPrincipalId, ProjectAccessRole.Admin);
        await seed.SaveChangesAsync();
        return project.ProjectId;
    }

    private sealed class DenyingRuntimePermissionAuthorization : IRuntimePermissionAuthorization
    {
        public Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
        {
            _ = permissionKey;
            _ = cancellationToken;
            throw new ForbiddenException("Insufficient permission.", "PERMISSION_DENIED");
        }
    }
}
