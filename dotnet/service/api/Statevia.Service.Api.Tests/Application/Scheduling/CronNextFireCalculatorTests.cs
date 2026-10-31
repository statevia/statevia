using Statevia.Core.Application.Scheduling;

namespace Statevia.Service.Api.Tests.Application.Scheduling;

/// <summary><see cref="CronNextFireCalculator"/> の固定時計と DST。</summary>
public sealed class CronNextFireCalculatorTests
{
    /// <summary>基準より後の最初の発生を UTC で返す。</summary>
    [Fact]
    public void NextStrictlyAfter_TokyoDaily_ReturnsNextUtc()
    {
        // Arrange
        var now = DateTime.SpecifyKind(new DateTime(2026, 9, 18, 17, 0, 0), DateTimeKind.Utc);

        // Act
        var next = CronNextFireCalculator.NextStrictlyAfter("0 3 * * *", "Asia/Tokyo", now);

        // Assert
        Assert.Equal(DateTimeKind.Utc, next.Kind);
        Assert.Equal(new DateTime(2026, 9, 18, 18, 0, 0, DateTimeKind.Utc), next);
    }

    /// <summary>夏時間への切替で欠損する時刻は飛ばし、次の実在時刻になる。</summary>
    [Fact]
    public void NextStrictlyAfter_SpringForward_SkipsGap()
    {
        // Arrange: America/New_York 2026-03-08 02:00 は存在しない
        var now = DateTime.SpecifyKind(new DateTime(2026, 3, 8, 6, 30, 0), DateTimeKind.Utc);

        // Act
        var next = CronNextFireCalculator.NextStrictlyAfter("30 2 * * *", "America/New_York", now);

        // Assert
        Assert.True(next > now);
        var local = TimeZoneInfo.ConvertTimeFromUtc(next, TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
        Assert.NotEqual(2, local.Hour);
    }

    /// <summary>標準時への切替で重複する時刻は、最初の出現を取る。</summary>
    [Fact]
    public void NextStrictlyAfter_FallBack_UsesFirstOccurrence()
    {
        // Arrange: 2026-11-01 01:00 が二度ある直前
        var now = DateTime.SpecifyKind(new DateTime(2026, 11, 1, 4, 30, 0), DateTimeKind.Utc);

        // Act
        var next = CronNextFireCalculator.NextStrictlyAfter("0 1 * * *", "America/New_York", now);

        // Assert
        Assert.Equal(DateTimeKind.Utc, next.Kind);
        Assert.True(next > now);
        Assert.True(next < now.AddHours(3));
    }

    /// <summary>不正 cron は ArgumentException。</summary>
    [Fact]
    public void NextStrictlyAfter_InvalidCron_ThrowsArgumentException()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            CronNextFireCalculator.NextStrictlyAfter("not-a-cron", "UTC", now));
        Assert.Equal("cronExpression", ex.ParamName);
    }

    /// <summary>不明 TZ は ArgumentException。</summary>
    [Fact]
    public void NextStrictlyAfter_UnknownTimeZone_ThrowsArgumentException()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            CronNextFireCalculator.NextStrictlyAfter("0 3 * * *", "Not/AZone", now));
        Assert.Equal("timeZoneId", ex.ParamName);
    }

    /// <summary>次枠が already now 以下なら欠発。</summary>
    [Fact]
    public void IsMissedSlot_WhenNextAlreadyPast_ReturnsTrue()
    {
        // Arrange: 毎日 03:00 JST。保存枠が 2 日前
        var saved = DateTime.SpecifyKind(new DateTime(2026, 9, 16, 18, 0, 0), DateTimeKind.Utc);
        var now = DateTime.SpecifyKind(new DateTime(2026, 9, 18, 18, 30, 0), DateTimeKind.Utc);

        // Act
        var missed = CronNextFireCalculator.IsMissedSlot("0 3 * * *", "Asia/Tokyo", saved, now);

        // Assert
        Assert.True(missed);
    }
}
