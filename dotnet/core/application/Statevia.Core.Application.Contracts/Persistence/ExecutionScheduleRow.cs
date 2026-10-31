namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>定期実行ジョブ（<c>schedules</c>）行。</summary>
public sealed class ExecutionScheduleRow
{
    /// <summary>スケジュール ID。</summary>
    public Guid ScheduleId { get; set; }

    /// <summary>テナント ID。</summary>
    public Guid TenantId { get; set; }

    /// <summary>システムジョブ識別子。テナントスケジュールは null。</summary>
    public string? JobKey { get; set; }

    /// <summary>対象定義。システム行は null。</summary>
    public Guid? DefinitionId { get; set; }

    /// <summary>ピンした定義版。null なら発火時 latest。</summary>
    public Guid? DefinitionVersionId { get; set; }

    /// <summary>run-as ServiceAccount の Principal ID。システム行は null。</summary>
    public Guid? RunAsPrincipalId { get; set; }

    /// <summary>作成した Principal。</summary>
    public Guid CreatedByPrincipalId { get; set; }

    /// <summary>テナント内表示名。</summary>
    public string Name { get; set; } = "";

    /// <summary>5 フィールド cron。</summary>
    public string CronExpression { get; set; } = "";

    /// <summary>IANA タイムゾーン。</summary>
    public string TimeZone { get; set; } = "";

    /// <summary><c>skip</c> または <c>allow</c>。</summary>
    public string OverlapPolicy { get; set; } = "skip";

    /// <summary>Start に渡す input JSON。機微。</summary>
    public string? InputJson { get; set; }

    /// <summary>Dispatcher が発火するか。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>論理削除時刻（UTC）。</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>次枠（UTC）。</summary>
    public DateTime NextFireAt { get; set; }

    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>更新日時（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>テナントスケジュールの定義 ID。</summary>
    /// <returns>定義 ID。</returns>
    /// <exception cref="InvalidOperationException">システム行など、定義が無い。</exception>
    public Guid RequireDefinitionId() =>
        DefinitionId ?? throw new InvalidOperationException("Tenant schedule requires definition_id.");

    /// <summary>テナントスケジュールの run-as Principal ID。</summary>
    /// <returns>Principal ID。</returns>
    /// <exception cref="InvalidOperationException">システム行など、run-as が無い。</exception>
    public Guid RequireRunAsPrincipalId() =>
        RunAsPrincipalId ?? throw new InvalidOperationException("Tenant schedule requires run_as_principal_id.");
}
