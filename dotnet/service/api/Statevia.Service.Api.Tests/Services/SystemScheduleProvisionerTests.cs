using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Contracts.Scheduling;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>システムスケジュール補完は既存行の発火時刻を変えない。</summary>
public sealed class SystemScheduleProvisionerTests
{
    /// <summary>同じテナント ID の分は再計算しても同じ。</summary>
    [Fact]
    public void MinuteOfHour_SameTenant_IsStable()
    {
        // Arrange
        var tenantId = Guid.Parse("018f6b2e-7c3a-7b2e-8c11-6a5d4e3f2b10");

        // Act
        var first = SystemScheduleRows.MinuteOfHour(tenantId);
        var second = SystemScheduleRows.MinuteOfHour(tenantId);

        // Assert
        Assert.Equal(first, second);
        Assert.InRange(first, 0, 59);
    }

    /// <summary>2 回補完しても行は 1 つで、next_fire_at は変わらない。</summary>
    [Fact]
    public async Task EnsureAsync_SecondCall_KeepsNextFireAt()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var repository = new ExecutionScheduleRepository(database.Factory);
        var provisioner = new SystemScheduleProvisioner(repository, new DefaultIdGenerator());
        var tenantId = TestTenantIds.DefaultTenantId;

        // Act
        await provisioner.EnsureAsync(tenantId, CancellationToken.None);
        await using var before = database.Factory.CreateDbContext();
        var first = await before.ExecutionSchedules.SingleAsync();
        var nextFireAt = first.NextFireAt;
        var cron = first.CronExpression;
        await provisioner.EnsureAsync(tenantId, CancellationToken.None);

        // Assert
        await using var after = database.Factory.CreateDbContext();
        var rows = await after.ExecutionSchedules.ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(SystemScheduleRows.StuckExecutionReportJobKey, row.JobKey);
        Assert.Equal(cron, row.CronExpression);
        Assert.Equal(nextFireAt, row.NextFireAt);
        Assert.Equal(SystemScheduleRows.CronExpression(tenantId), row.CronExpression);
    }
}
