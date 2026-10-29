using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary><see cref="ExecutionScheduleRepository"/> の一意制約と、閲覧できるスケジュールの絞り込み。</summary>
public sealed class ExecutionScheduleRepositoryTests
{
    /// <summary>同一スケジュールの同一枠は二重 INSERT できない。</summary>
    [Fact]
    public async Task AddRunAsync_DuplicateSlot_Throws()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var repository = new ExecutionScheduleRepository(database.Factory);
        var scheduleId = Guid.NewGuid();
        var tenantId = TestTenantIds.DefaultTenantId;
        var slot = DateTime.SpecifyKind(new DateTime(2026, 9, 19, 3, 0, 0), DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        await repository.AddAsync(
            new ExecutionScheduleRow
            {
                ScheduleId = scheduleId,
                TenantId = tenantId,
                DefinitionId = Guid.NewGuid(),
                RunAsPrincipalId = Guid.NewGuid(),
                CreatedByPrincipalId = Guid.NewGuid(),
                Name = "nightly",
                CronExpression = "0 3 * * *",
                TimeZone = "Asia/Tokyo",
                OverlapPolicy = "skip",
                Enabled = true,
                NextFireAt = slot,
                CreatedAt = now,
                UpdatedAt = now
            },
            CancellationToken.None);
        await repository.AddRunAsync(
            new ExecutionScheduleRunRow
            {
                ScheduleRunId = Guid.NewGuid(),
                ScheduleId = scheduleId,
                TenantId = tenantId,
                ScheduledFireAt = slot,
                Manual = false,
                Outcome = "started",
                CreatedAt = now
            },
            CancellationToken.None);

        // Act
        var act = () => repository.AddRunAsync(
            new ExecutionScheduleRunRow
            {
                ScheduleRunId = Guid.NewGuid(),
                ScheduleId = scheduleId,
                TenantId = tenantId,
                ScheduledFireAt = slot,
                Manual = false,
                Outcome = "started",
                CreatedAt = now
            },
            CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(act);
    }

    /// <summary>一覧と ID 取得はシステムスケジュールを不在と同じにする。</summary>
    [Fact]
    public async Task ListVisibleAsync_OmitsSystemSchedule()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var repository = new ExecutionScheduleRepository(database.Factory);
        var tenantId = TestTenantIds.DefaultTenantId;
        var seeded = await SeedVisibleDefinitionAsync(database, ProjectAccessRole.Reader);
        var now = DateTime.UtcNow;
        var systemId = Guid.NewGuid();
        await repository.AddAsync(
            new ExecutionScheduleRow
            {
                ScheduleId = systemId,
                TenantId = tenantId,
                JobKey = "stuck-execution-report",
                Name = "stuck-execution-report",
                CronExpression = "15 * * * *",
                TimeZone = "UTC",
                OverlapPolicy = "skip",
                Enabled = true,
                NextFireAt = now,
                CreatedAt = now,
                UpdatedAt = now
            },
            CancellationToken.None);
        await repository.AddAsync(NewTenantSchedule("visible", seeded.DefinitionId), CancellationToken.None);

        // Act
        var listed = await repository.ListVisibleAsync(tenantId, seeded.PrincipalId, CancellationToken.None);
        var hidden = await repository.GetByIdAsync(systemId, CancellationToken.None);

        // Assert
        Assert.Equal(["visible"], listed.Select(row => row.Name).ToArray());
        Assert.Null(hidden);
    }

    /// <summary>定義の Project に所属しない Principal にはスケジュールを返さない。</summary>
    [Fact]
    public async Task ListVisibleAsync_WhenCallerNotMember_ReturnsEmpty()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var repository = new ExecutionScheduleRepository(database.Factory);
        var seeded = await SeedVisibleDefinitionAsync(database, ProjectAccessRole.Reader);
        await repository.AddAsync(NewTenantSchedule("other-project", seeded.DefinitionId), CancellationToken.None);

        // Act
        var listed = await repository.ListVisibleAsync(
            TestTenantIds.DefaultTenantId,
            Guid.NewGuid(),
            CancellationToken.None);

        // Assert
        Assert.Empty(listed);
    }

    /// <summary>実行リソース許可の外の定義を使うスケジュールは返さない。</summary>
    [Fact]
    public async Task ListVisibleAsync_WhenDefinitionOutsideGrant_OmitsRow()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var repository = new ExecutionScheduleRepository(database.Factory);
        var seeded = await SeedVisibleDefinitionAsync(database, ProjectAccessRole.Reader);
        await using (var ctx = database.Factory.CreateDbContext())
        {
            ctx.PrincipalResourceGrants.Add(new PrincipalResourceGrantRow
            {
                TenantId = TestTenantIds.DefaultTenantId,
                PrincipalId = seeded.PrincipalId,
                ResourceKind = PrincipalResourceGrantKinds.Definition,
                ResourceId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        await repository.AddAsync(NewTenantSchedule("outside-grant", seeded.DefinitionId), CancellationToken.None);

        // Act
        var listed = await repository.ListVisibleAsync(
            TestTenantIds.DefaultTenantId,
            seeded.PrincipalId,
            CancellationToken.None);

        // Assert
        Assert.Empty(listed);
    }

    /// <summary>定義が論理削除されても、所属 Project のメンバーにはスケジュールを返す。</summary>
    [Fact]
    public async Task ListVisibleAsync_WhenDefinitionDeleted_KeepsRowForMember()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var repository = new ExecutionScheduleRepository(database.Factory);
        var seeded = await SeedVisibleDefinitionAsync(database, ProjectAccessRole.Reader);
        await repository.AddAsync(NewTenantSchedule("orphan", seeded.DefinitionId), CancellationToken.None);
        await using (var ctx = database.Factory.CreateDbContext())
        {
            var definition = await ctx.Definitions.SingleAsync(row => row.DefinitionId == seeded.DefinitionId);
            definition.DeletedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // Act
        var listed = await repository.ListVisibleAsync(
            TestTenantIds.DefaultTenantId,
            seeded.PrincipalId,
            CancellationToken.None);

        // Assert
        Assert.Equal(["orphan"], listed.Select(row => row.Name).ToArray());
    }

    /// <summary>既定テナントに Project・定義・メンバーを作る。</summary>
    private static async Task<(Guid DefinitionId, Guid PrincipalId)> SeedVisibleDefinitionAsync(
        SqliteTestDatabase database,
        ProjectAccessRole role)
    {
        var tenantId = TestTenantIds.DefaultTenantId;
        var projectId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        await using var ctx = database.Factory.CreateDbContext();
        ProjectTestData.AddDefaultProject(ctx, tenantId, "default", projectId);
        var principalId = ProjectTestData.AddMemberPrincipal(ctx, tenantId, projectId, role);
        DefinitionTestData.AddDefinitionWithVersion(ctx, tenantId, definitionId, "wf-schedule", projectId);
        await ctx.SaveChangesAsync();
        return (definitionId, principalId);
    }

    private static ExecutionScheduleRow NewTenantSchedule(string name, Guid definitionId)
    {
        var now = DateTime.UtcNow;
        return new ExecutionScheduleRow
        {
            ScheduleId = Guid.NewGuid(),
            TenantId = TestTenantIds.DefaultTenantId,
            DefinitionId = definitionId,
            RunAsPrincipalId = Guid.NewGuid(),
            CreatedByPrincipalId = Guid.NewGuid(),
            Name = name,
            CronExpression = "0 3 * * *",
            TimeZone = "UTC",
            OverlapPolicy = "skip",
            Enabled = true,
            NextFireAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
