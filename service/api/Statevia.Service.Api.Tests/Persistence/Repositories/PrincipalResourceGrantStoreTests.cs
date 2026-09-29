using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary><see cref="PrincipalResourceGrantStore"/> の一意制約とテナント境界。</summary>
public sealed class PrincipalResourceGrantStoreTests
{
    /// <summary>同一 Principal・種別・リソースの二重 INSERT は失敗する。</summary>
    [Fact]
    public async Task Insert_DuplicatePrincipalKindResource_Throws()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        await using (var db = database.Factory.CreateDbContext())
        {
            AddPrincipal(db, principalId, TestTenantIds.DefaultTenantId);
            db.PrincipalResourceGrants.Add(Grant(TestTenantIds.DefaultTenantId, principalId, PrincipalResourceGrantKinds.Project, resourceId));
            await db.SaveChangesAsync();
        }

        // Act
        await using var again = database.Factory.CreateDbContext();
        again.PrincipalResourceGrants.Add(Grant(TestTenantIds.DefaultTenantId, principalId, PrincipalResourceGrantKinds.Project, resourceId));
        var act = () => again.SaveChangesAsync();

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(act);
    }

    /// <summary>一覧は解決済みテナントの行だけを返す。</summary>
    [Fact]
    public async Task ListAsync_OtherTenant_IsHidden()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = Guid.NewGuid();
        var ownResourceId = Guid.NewGuid();
        var otherResourceId = Guid.NewGuid();
        await using (var db = database.Factory.CreateDbContext())
        {
            AddPrincipal(db, principalId, TestTenantIds.DefaultTenantId);
            db.PrincipalResourceGrants.Add(Grant(TestTenantIds.DefaultTenantId, principalId, PrincipalResourceGrantKinds.Project, ownResourceId));
            db.PrincipalResourceGrants.Add(Grant(TestTenantIds.T1TenantId, principalId, PrincipalResourceGrantKinds.Project, otherResourceId));
            await db.SaveChangesAsync();
        }

        var store = new PrincipalResourceGrantStore(database.Factory, database.TenantAccessor);

        // Act
        var rows = await store.ListAsync(principalId, CancellationToken.None);

        // Assert
        var row = Assert.Single(rows);
        Assert.Equal(TestTenantIds.DefaultTenantId, row.TenantId);
        Assert.Equal(ownResourceId, row.ResourceId);
    }

    /// <summary>QueryFilter 有効時も他テナントの許可行は見えない。</summary>
    [Fact]
    public async Task QueryFilter_OtherTenant_IsHidden()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = Guid.NewGuid();
        await using (var seed = new CoreDbContext(database.Options, database.TenantAccessor, DisabledTenantQueryFilterOptions.Instance))
        {
            AddPrincipal(seed, principalId, TestTenantIds.DefaultTenantId);
            seed.PrincipalResourceGrants.Add(Grant(TestTenantIds.DefaultTenantId, principalId, PrincipalResourceGrantKinds.Definition, Guid.NewGuid()));
            seed.PrincipalResourceGrants.Add(Grant(TestTenantIds.T1TenantId, principalId, PrincipalResourceGrantKinds.Definition, Guid.NewGuid()));
            await seed.SaveChangesAsync();
        }

        database.TenantAccessor.Set(TestTenantIds.DefaultContext);

        // Act
        await using var db = new CoreDbContext(database.Options, database.TenantAccessor, EnabledTenantQueryFilterOptions.Instance);
        var rows = await db.PrincipalResourceGrants.AsNoTracking().ToListAsync();

        // Assert
        var row = Assert.Single(rows);
        Assert.Equal(TestTenantIds.DefaultTenantId, row.TenantId);
    }

    /// <summary>両種別を一括置換し、重複 ID は 1 行にする。</summary>
    [Fact]
    public async Task ReplaceAsync_BothKinds_DeduplicatesAndReplaces()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var removedDefinitionId = Guid.NewGuid();
        await using (var db = database.Factory.CreateDbContext())
        {
            AddPrincipal(db, principalId, TestTenantIds.DefaultTenantId);
            db.PrincipalResourceGrants.Add(Grant(
                TestTenantIds.DefaultTenantId,
                principalId,
                PrincipalResourceGrantKinds.Definition,
                removedDefinitionId));
            await db.SaveChangesAsync();
        }

        var store = new PrincipalResourceGrantStore(database.Factory, database.TenantAccessor);

        // Act
        await store.ReplaceAsync(
            principalId,
            [projectId, projectId],
            [],
            CancellationToken.None);
        var rows = await store.ListAsync(principalId, CancellationToken.None);

        // Assert
        var row = Assert.Single(rows);
        Assert.Equal(PrincipalResourceGrantKinds.Project, row.ResourceKind);
        Assert.Equal(projectId, row.ResourceId);
    }

    /// <summary>テナント未解決の置換は保存しない。</summary>
    [Fact]
    public async Task ReplaceAsync_UnresolvedTenant_Throws()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        database.TenantAccessor.Set(null);
        var store = new PrincipalResourceGrantStore(database.Factory, database.TenantAccessor);

        // Act
        var act = () => store.ReplaceAsync(Guid.NewGuid(), [], [], CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    private static void AddPrincipal(CoreDbContext db, Guid principalId, Guid tenantId)
    {
        var now = DateTime.UtcNow;
        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = tenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.User,
            DisplayName = "grant-test",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static PrincipalResourceGrantRow Grant(Guid tenantId, Guid principalId, string kind, Guid resourceId) =>
        new()
        {
            TenantId = tenantId,
            PrincipalId = principalId,
            ResourceKind = kind,
            ResourceId = resourceId,
            CreatedAt = DateTime.UtcNow
        };
}
