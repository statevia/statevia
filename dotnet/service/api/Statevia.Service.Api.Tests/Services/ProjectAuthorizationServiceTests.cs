using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ProjectAuthorizationService"/> の検証。</summary>
public sealed class ProjectAuthorizationServiceTests
{
    /// <summary>Reader のメンバーは読み取りを許可する。</summary>
    [Fact]
    public async Task EnsureCanReadAsync_WithReaderRole_DoesNotThrow()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var seeded = await SeedMembershipAsync(db, ProjectAccessRole.Reader);
        var service = CreateService(seeded.TenantId, seeded.PrincipalId);
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var exception = await Record.ExceptionAsync(() =>
            service.EnsureCanReadAsync(uow, seeded.TenantId, seeded.ProjectId, default));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Reader が Execute を試行すると 403 になる。</summary>
    [Fact]
    public async Task EnsureCanExecuteAsync_ReaderOnly_ThrowsForbidden()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var seeded = await SeedMembershipAsync(db, ProjectAccessRole.Reader);
        var service = CreateService(seeded.TenantId, seeded.PrincipalId);
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.EnsureCanExecuteAsync(uow, seeded.TenantId, seeded.ProjectId, default));

        // Assert
        Assert.Equal("PROJECT_ACCESS_DENIED", ex.Code);
    }

    /// <summary>Executor のメンバーは Execute を許可する。</summary>
    [Fact]
    public async Task EnsureCanExecuteAsync_WithExecutorRole_DoesNotThrow()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var seeded = await SeedMembershipAsync(db, ProjectAccessRole.Executor);
        var service = CreateService(seeded.TenantId, seeded.PrincipalId);
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var exception = await Record.ExceptionAsync(() =>
            service.EnsureCanExecuteAsync(uow, seeded.TenantId, seeded.ProjectId, default));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Publisher のメンバーは publish を許可する。</summary>
    [Fact]
    public async Task EnsureCanPublishAsync_WithPublisherRole_DoesNotThrow()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var seeded = await SeedMembershipAsync(db, ProjectAccessRole.Publisher);
        var service = CreateService(seeded.TenantId, seeded.PrincipalId);
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var exception = await Record.ExceptionAsync(() =>
            service.EnsureCanPublishAsync(uow, seeded.TenantId, seeded.ProjectId, default));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Executor が publish を試行すると 403 になる。</summary>
    [Fact]
    public async Task EnsureCanPublishAsync_ExecutorOnly_ThrowsForbidden()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var seeded = await SeedMembershipAsync(db, ProjectAccessRole.Executor);
        var service = CreateService(seeded.TenantId, seeded.PrincipalId);
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.EnsureCanPublishAsync(uow, seeded.TenantId, seeded.ProjectId, default));

        // Assert
        Assert.Equal("PROJECT_ACCESS_DENIED", ex.Code);
    }

    /// <summary>メンバー行が無い Principal の読み取りは 404。</summary>
    [Fact]
    public async Task EnsureCanReadAsync_NoMembership_ThrowsNotFound()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var seeded = await SeedMembershipAsync(db, role: null);
        var service = CreateService(seeded.TenantId, seeded.PrincipalId);
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.EnsureCanReadAsync(uow, seeded.TenantId, seeded.ProjectId, default));

        // Assert
        Assert.NotNull(ex);
    }

    /// <summary>テナント管理者は未所属でもメンバー管理できる。</summary>
    [Fact]
    public async Task EnsureCanManageMembersAsync_TenantAdminWithoutMembership_DoesNotThrow()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var seeded = await SeedMembershipAsync(db, role: null);
        var service = CreateService(seeded.TenantId, seeded.PrincipalId, isTenantAdmin: true);
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var exception = await Record.ExceptionAsync(() =>
            service.EnsureCanManageMembersAsync(uow, seeded.TenantId, seeded.ProjectId, default));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Reader のメンバー管理は 403。</summary>
    [Fact]
    public async Task EnsureCanManageMembersAsync_Reader_ThrowsForbidden()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var seeded = await SeedMembershipAsync(db, ProjectAccessRole.Reader);
        var service = CreateService(seeded.TenantId, seeded.PrincipalId);
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.EnsureCanManageMembersAsync(uow, seeded.TenantId, seeded.ProjectId, default));

        // Assert
        Assert.Equal("PROJECT_ACCESS_DENIED", ex.Code);
    }

    private static ProjectAuthorizationService CreateService(Guid tenantId, Guid principalId, bool isTenantAdmin = false) =>
        new(
            new ProjectRepository(new DefaultIdGenerator()),
            new FixedTenantContextAccessor(new TenantContextState(
                tenantId,
                "tenant",
                principalId,
                TenantLifecycle.Active)),
            new StubTenantAdminAuthorization(isTenantAdmin));

    private static async Task<(Guid TenantId, Guid PrincipalId, Guid ProjectId)> SeedMembershipAsync(
        SqliteTestDatabase db,
        ProjectAccessRole? role)
    {
        var tenantId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using var seed = db.Factory.CreateDbContext();
        seed.Tenants.Add(new TenantRow
        {
            TenantId = tenantId,
            TenantKey = "tenant",
            DisplayName = "Tenant",
            Lifecycle = TenantLifecycle.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        ProjectTestData.AddDefaultProject(seed, tenantId, "tenant", projectId);
        if (role is { } grantedRole)
        {
            ProjectTestData.AddPrincipal(seed, tenantId, principalId);
            ProjectTestData.GrantAccess(seed, projectId, principalId, grantedRole);
        }
        await seed.SaveChangesAsync();

        return (tenantId, principalId, projectId);
    }

    private sealed class StubTenantAdminAuthorization(bool isTenantAdmin) : ITenantAdminAuthorization
    {
        public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken)
        {
            _ = principalId;
            _ = cancellationToken;
            return Task.FromResult(isTenantAdmin);
        }
    }
}
