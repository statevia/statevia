using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

    /// <summary>未所属の tenant.admin が自分を admin にできる。監査に主体・Project・対象が出る。</summary>
    [Fact]
    public async Task UpsertMemberAsync_TenantAdminWithoutMembership_GrantsSelfAndAudits()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        var logger = new CollectingLogger();
        var sut = CreateSut(
            db,
            OtherPrincipalId,
            tenantAdmin: new AllowTenantAdminAuthorization(),
            logger: logger);
        await using (var seed = new CoreDbContext(db.Options))
        {
            ProjectTestData.AddPrincipal(seed, TestTenantIds.DefaultTenantId, OtherPrincipalId);
            await seed.SaveChangesAsync();
        }

        // Act
        await sut.UpsertMemberAsync(
            projectId,
            OtherPrincipalId,
            new UpsertProjectMemberRequest { Role = "admin" },
            CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var access = await verify.ProjectAccesses.SingleAsync(
            a => a.ProjectId == projectId && a.PrincipalId == OtherPrincipalId);
        Assert.Equal(ProjectAccessRole.Admin, access.Role);
        var message = Assert.Single(logger.Messages);
        Assert.Contains("ActorPrincipalId=", message, StringComparison.Ordinal);
        Assert.Contains("ProjectId=", message, StringComparison.Ordinal);
        Assert.Contains("TargetPrincipalId=", message, StringComparison.Ordinal);
        Assert.DoesNotContain("password", message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>他テナント Principal の付与は 422。</summary>
    [Fact]
    public async Task UpsertMemberAsync_OtherTenantPrincipal_ThrowsApiValidation()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        await using (var seed = new CoreDbContext(db.Options))
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = TestTenantIds.OtherTenantId,
                TenantKey = "other",
                DisplayName = "Other",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            ProjectTestData.AddPrincipal(seed, TestTenantIds.OtherTenantId, OtherPrincipalId);
            await seed.SaveChangesAsync();
        }

        var sut = CreateSut(db, CreatorPrincipalId);

        // Act & Assert
        await Assert.ThrowsAsync<ApiValidationException>(() =>
            sut.UpsertMemberAsync(
                projectId,
                OtherPrincipalId,
                new UpsertProjectMemberRequest { Role = "reader" },
                CancellationToken.None));
    }

    /// <summary>project.admin は最後の admin を外せない。</summary>
    [Fact]
    public async Task RemoveMemberAsync_LastAdminByProjectAdmin_ThrowsApiValidation()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        var sut = CreateSut(db, CreatorPrincipalId);

        // Act & Assert
        await Assert.ThrowsAsync<ApiValidationException>(() =>
            sut.RemoveMemberAsync(projectId, CreatorPrincipalId, CancellationToken.None));
        await using var verify = new CoreDbContext(db.Options);
        Assert.Equal(
            ProjectAccessRole.Admin,
            await verify.ProjectAccesses.Where(a => a.ProjectId == projectId).Select(a => a.Role).SingleAsync());
    }

    /// <summary>tenant.admin は最後の admin を外せる。</summary>
    [Fact]
    public async Task RemoveMemberAsync_LastAdminByTenantAdmin_Removes()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        await using (var seed = new CoreDbContext(db.Options))
        {
            ProjectTestData.AddPrincipal(seed, TestTenantIds.DefaultTenantId, OtherPrincipalId);
            await seed.SaveChangesAsync();
        }

        var sut = CreateSut(db, OtherPrincipalId, tenantAdmin: new AllowTenantAdminAuthorization());

        // Act
        await sut.RemoveMemberAsync(projectId, CreatorPrincipalId, CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        Assert.Empty(await verify.ProjectAccesses.Where(a => a.ProjectId == projectId).ToListAsync());
    }

    /// <summary>project.admin は最後の admin を降格できない。</summary>
    [Fact]
    public async Task UpsertMemberAsync_DemoteLastAdmin_ThrowsApiValidation()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        var sut = CreateSut(db, CreatorPrincipalId);

        // Act & Assert
        await Assert.ThrowsAsync<ApiValidationException>(() =>
            sut.UpsertMemberAsync(
                projectId,
                CreatorPrincipalId,
                new UpsertProjectMemberRequest { Role = "reader" },
                CancellationToken.None));
    }

    /// <summary>メンバー一覧は表示名と種別を返す。</summary>
    [Fact]
    public async Task ListMembersAsync_Admin_ReturnsDisplayName()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        var sut = CreateSut(db, CreatorPrincipalId);

        // Act
        var members = await sut.ListMembersAsync(projectId, CancellationToken.None);

        // Assert
        var member = Assert.Single(members);
        Assert.Equal(CreatorPrincipalId, member.PrincipalId);
        Assert.Equal("principal", member.DisplayName);
        Assert.Equal("user", member.Kind);
        Assert.Equal("admin", member.Role);
    }

    /// <summary>付与候補は同一テナントのユーザーと API キーだけ。</summary>
    [Fact]
    public async Task ListGrantCandidatesAsync_IncludesUsersAndApiKeysOnly()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        var apiKeyPrincipalId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var serviceAccountId = Guid.Parse("55555555-5555-4555-8555-555555555555");
        var outsiderId = Guid.Parse("77777777-7777-4777-8777-777777777777");
        var otherTenantId = Guid.Parse("66666666-6666-4666-8666-666666666666");
        var now = DateTime.UtcNow;
        await using (var seed = new CoreDbContext(db.Options))
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = otherTenantId,
                TenantKey = "other",
                DisplayName = "Other",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
            AddServicePrincipal(seed, TestTenantIds.DefaultTenantId, apiKeyPrincipalId, "ci-key", now);
            AddServicePrincipal(seed, TestTenantIds.DefaultTenantId, serviceAccountId, "worker", now);
            ProjectTestData.AddPrincipal(seed, otherTenantId, outsiderId);
            seed.ApiKeys.Add(new ApiKeyRow
            {
                ApiKeyId = Guid.NewGuid(),
                TenantId = TestTenantIds.DefaultTenantId,
                PrincipalId = apiKeyPrincipalId,
                KeyPrefix = "sv_test",
                KeyHash = "hash",
                AllowedScopesJson = "[]",
                CreatedAt = now
            });
            await seed.SaveChangesAsync();
        }

        var sut = CreateSut(db, CreatorPrincipalId);

        // Act
        var candidates = await sut.ListGrantCandidatesAsync(projectId, CancellationToken.None);

        // Assert
        Assert.Contains(candidates, candidate =>
            candidate.PrincipalId == CreatorPrincipalId
            && candidate.DisplayName == "principal"
            && candidate.Kind == "user");
        Assert.Contains(candidates, candidate =>
            candidate.PrincipalId == apiKeyPrincipalId
            && candidate.DisplayName == "ci-key"
            && candidate.Kind == "apiKey");
        Assert.DoesNotContain(candidates, candidate => candidate.PrincipalId == serviceAccountId);
        Assert.DoesNotContain(candidates, candidate => candidate.PrincipalId == outsiderId);
    }

    /// <summary>reader はメンバー一覧を取れない。</summary>
    [Fact]
    public async Task ListMembersAsync_Reader_ThrowsForbidden()
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
            sut.ListMembersAsync(projectId, CancellationToken.None));
        Assert.Equal("PROJECT_ACCESS_DENIED", ex.Code);
    }

    /// <summary>tenant.admin の全件一覧は未所属 Project を含む。</summary>
    [Fact]
    public async Task ListAllForTenantAdminAsync_IncludesProjectsWithoutMembership()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var projectId = await SeedTenantWithProjectAsync(db, grantCreatorAdmin: false);
        await using (var seed = new CoreDbContext(db.Options))
        {
            ProjectTestData.AddPrincipal(seed, TestTenantIds.DefaultTenantId, OtherPrincipalId);
            await seed.SaveChangesAsync();
        }

        var sut = CreateSut(db, OtherPrincipalId, tenantAdmin: new AllowTenantAdminAuthorization());

        // Act
        var list = await sut.ListAllForTenantAdminAsync(CancellationToken.None);

        // Assert
        var item = Assert.Single(list);
        Assert.Equal(projectId, item.ProjectId);
        Assert.Null(item.Role);
    }

    /// <summary>tenant.admin 以外の全件一覧は 403。</summary>
    [Fact]
    public async Task ListAllForTenantAdminAsync_NonAdmin_ThrowsForbidden()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        await SeedTenantWithProjectAsync(db, grantCreatorAdmin: true);
        var sut = CreateSut(db, CreatorPrincipalId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.ListAllForTenantAdminAsync(CancellationToken.None));
        Assert.Equal("FORBIDDEN", ex.Code);
    }

    private static ProjectService CreateSut(
        InMemoryTestDatabase db,
        Guid principalId,
        bool denyProjectsCreate = false,
        ITenantContextAccessor? tenant = null,
        ITenantAdminAuthorization? tenantAdmin = null,
        ILogger<ProjectService>? logger = null)
    {
        var tenantAccessor = tenant ?? new FixedTenantContextAccessor(
            TestTenantIds.DefaultContext with { PrincipalId = principalId });
        var projectRepo = new ProjectRepository(new DefaultIdGenerator());
        var executor = new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory));
        IRuntimePermissionAuthorization runtimeAuth = denyProjectsCreate
            ? new DenyingRuntimePermissionAuthorization()
            : new AllowAllRuntimePermissionAuthorization();
        var admin = tenantAdmin ?? new DenyTenantAdminAuthorization();

        return new ProjectService(
            projectRepo,
            new ProjectAuthorizationService(projectRepo, tenantAccessor, admin),
            tenantAccessor,
            runtimeAuth,
            new DefaultIdGenerator(),
            executor,
            admin,
            logger ?? NullLogger<ProjectService>.Instance);
    }

    private static void AddServicePrincipal(
        CoreDbContext seed,
        Guid tenantId,
        Guid principalId,
        string displayName,
        DateTime now)
    {
        seed.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = tenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.ServiceAccount,
            DisplayName = displayName,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
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

    private sealed class AllowTenantAdminAuthorization : ITenantAdminAuthorization
    {
        public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class CollectingLogger : ILogger<ProjectService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
