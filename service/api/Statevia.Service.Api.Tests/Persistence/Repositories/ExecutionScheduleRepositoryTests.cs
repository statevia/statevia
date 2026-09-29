using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary><see cref="ExecutionScheduleRepository"/> の一意制約。</summary>
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

    /// <summary>一覧と ID 取得はシステム行を不在と同じにする。</summary>
    [Fact]
    public async Task ListAsync_OmitsSystemSchedule()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var repository = new ExecutionScheduleRepository(database.Factory);
        var tenantId = TestTenantIds.DefaultTenantId;
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
        await repository.AddAsync(
            new ExecutionScheduleRow
            {
                ScheduleId = Guid.NewGuid(),
                TenantId = tenantId,
                DefinitionId = Guid.NewGuid(),
                RunAsPrincipalId = Guid.NewGuid(),
                CreatedByPrincipalId = Guid.NewGuid(),
                Name = "visible",
                CronExpression = "0 3 * * *",
                TimeZone = "UTC",
                OverlapPolicy = "skip",
                Enabled = true,
                NextFireAt = now,
                CreatedAt = now,
                UpdatedAt = now
            },
            CancellationToken.None);

        // Act
        var listed = await repository.ListAsync(CancellationToken.None);
        var hidden = await repository.GetByIdAsync(systemId, CancellationToken.None);

        // Assert
        Assert.Equal(["visible"], listed.Select(row => row.Name).ToArray());
        Assert.Null(hidden);
    }
}
