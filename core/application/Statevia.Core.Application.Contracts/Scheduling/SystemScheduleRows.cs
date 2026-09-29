using Statevia.Core.Application.Contracts.Persistence;
using System.Security.Cryptography;
using System.Text;

namespace Statevia.Core.Application.Contracts.Scheduling;

/// <summary>
/// テナントに見せないシステムスケジュール行を作る。
/// </summary>
/// <remarks>
/// <para>分はテナント ID（<c>D</c> 形式・小文字）の SHA-256 先頭バイトを 60 で割った余り。再補完では既存行を上書きしない。</para>
/// <para>作成者 Principal は持たない。列は NOT NULL のため <see cref="Guid.Empty"/> を入れる。</para>
/// </remarks>
public static class SystemScheduleRows
{
    /// <summary>停滞と失敗枠を数えるジョブ。</summary>
    public const string StuckExecutionReportJobKey = "stuck-execution-report";

    /// <summary>テナントが Active でないときの <c>schedule_runs.error_code</c>。</summary>
    public const string TenantNotActiveErrorCode = "TENANT_NOT_ACTIVE";

    /// <summary>点検不能のときの <c>schedule_runs.error_code</c>。</summary>
    public const string ReportFailedErrorCode = "REPORT_FAILED";

    /// <summary>テナント ID から毎時の分（0〜59）を決める。</summary>
    /// <param name="tenantId">テナント ID。</param>
    /// <returns>cron の分。</returns>
    public static int MinuteOfHour(Guid tenantId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(tenantId.ToString("D")));
        return hash[0] % 60;
    }

    /// <summary>毎時 cron。</summary>
    /// <param name="tenantId">テナント ID。</param>
    /// <returns><c>{minute} * * * *</c>。</returns>
    public static string CronExpression(Guid tenantId) => $"{MinuteOfHour(tenantId)} * * * *";

    /// <summary>UTC で、指定分の次の正時枠。</summary>
    /// <param name="minute">0〜59。</param>
    /// <param name="utcNow">現在 UTC。</param>
    /// <returns>現在より後の、その分の UTC 時刻。</returns>
    public static DateTime NextFireAtUtc(int minute, DateTime utcNow)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minute);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minute, 59);
        var utc = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var candidate = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, minute, 0, DateTimeKind.Utc);
        if (candidate <= utc)
            candidate = candidate.AddHours(1);
        return candidate;
    }

    /// <summary>新規のシステムスケジュール行。定義も run-as も input も持たない。</summary>
    /// <param name="scheduleId">スケジュール ID。</param>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="utcNow">作成時刻（UTC）。</param>
    /// <returns>未保存の行。</returns>
    public static ExecutionScheduleRow Create(Guid scheduleId, Guid tenantId, DateTime utcNow)
    {
        var minute = MinuteOfHour(tenantId);
        var now = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return new ExecutionScheduleRow
        {
            ScheduleId = scheduleId,
            TenantId = tenantId,
            JobKey = StuckExecutionReportJobKey,
            DefinitionId = null,
            RunAsPrincipalId = null,
            CreatedByPrincipalId = Guid.Empty,
            Name = StuckExecutionReportJobKey,
            CronExpression = $"{minute} * * * *",
            TimeZone = "UTC",
            OverlapPolicy = "skip",
            InputJson = null,
            Enabled = true,
            NextFireAt = NextFireAtUtc(minute, now),
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
