using Cronos;

namespace Statevia.Core.Application.Scheduling;

/// <summary>5 フィールド cron と IANA TZ から次の UTC 発火時刻を求める。</summary>
/// <remarks>
/// <para>Hangfire / Quartz は使わない。解釈は Cronos に固定する。</para>
    /// <para>DST: 夏時間への切替で欠損する時刻は次の実在時刻、標準時への切替で重複する時刻は最初の出現（標準時側）を採用する。</para>
/// </remarks>
public static class CronNextFireCalculator
{
    /// <summary><paramref name="utcNow"/> より後の最初の発生を UTC で返す。</summary>
    /// <param name="cronExpression">5 フィールド cron。</param>
    /// <param name="timeZoneId">IANA ゾーン。</param>
    /// <param name="utcNow">基準時刻（UTC）。この時刻自体は含めない。</param>
    /// <returns>次枠の UTC。</returns>
    /// <exception cref="ArgumentException">cron または TZ が不正。</exception>
    /// <exception cref="InvalidOperationException">次発生が無い（終端 cron）。</exception>
    public static DateTime NextStrictlyAfter(string cronExpression, string timeZoneId, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        if (!CronExpression.TryParse(cronExpression.Trim(), CronFormat.Standard, out var cron) || cron is null)
            throw new ArgumentException("cronExpression must be a 5-field cron.", nameof(cronExpression));

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException("timeZone must be a valid IANA identifier.", nameof(timeZoneId), ex);
        }

        var fromUtc = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var next = cron.GetNextOccurrence(fromUtc, timeZone);
        if (next is null)
            throw new InvalidOperationException("cron expression has no next occurrence.");

        return DateTime.SpecifyKind(next.Value, DateTimeKind.Utc);
    }

    /// <summary>保存枠 T の次枠 T' が既に now 以下なら欠発。</summary>
    /// <param name="cronExpression">5 フィールド cron。</param>
    /// <param name="timeZoneId">IANA ゾーン。</param>
    /// <param name="scheduledFireAtUtc">保存されている枠 T。</param>
    /// <param name="utcNow">現在 UTC。</param>
    /// <returns>欠発なら true。</returns>
    public static bool IsMissedSlot(
        string cronExpression,
        string timeZoneId,
        DateTime scheduledFireAtUtc,
        DateTime utcNow)
    {
        var nextAfterSaved = NextStrictlyAfter(cronExpression, timeZoneId, scheduledFireAtUtc);
        return nextAfterSaved <= utcNow;
    }
}
