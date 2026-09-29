using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Statevia.Core.Application.Contracts.Validation;

namespace Statevia.Core.Application.Contracts.Services;

/// <summary>定期実行スケジュールの CRUD。</summary>
/// <remarks>
/// <para>認可は <c>executions.read</c> / <c>executions.write</c> と定義の project executor。手動 <c>/run</c> も同じ書き込み認可。</para>
/// <para>一覧は機微 <c>input</c> を含めない。ジョブ作成で ServiceAccount は発行しない。</para>
/// </remarks>
public interface IExecutionScheduleService
{
    /// <summary>テナント内の未削除スケジュールを一覧する。</summary>
    Task<IReadOnlyList<ExecutionScheduleListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>未削除の 1 件を取得する。</summary>
    /// <exception cref="NotFoundException">無い、または他テナント。</exception>
    Task<ExecutionScheduleDetailDto> GetAsync(Guid scheduleId, CancellationToken cancellationToken);

    /// <summary>スケジュールを作成し <c>next_fire_at</c> を計算する。</summary>
    /// <exception cref="ApiValidationException">cron / TZ / run-as / overlap / 名前が不正。</exception>
    /// <exception cref="NotFoundException">定義または指定版が無い。</exception>
    Task<ExecutionScheduleDetailDto> CreateAsync(
        CreateExecutionScheduleRequest request,
        CancellationToken cancellationToken);

    /// <summary>未削除行を部分更新する。cron / TZ 変更時は next を再計算する。</summary>
    Task<ExecutionScheduleDetailDto> UpdateAsync(
        Guid scheduleId,
        UpdateExecutionScheduleRequest request,
        CancellationToken cancellationToken);

    /// <summary>論理削除する。以降 Dispatcher は発火しない。</summary>
    Task DeleteAsync(Guid scheduleId, CancellationToken cancellationToken);

    /// <summary>
    /// 手動で 1 回 Start する。cron の <c>next_fire_at</c> は変えない。
    /// </summary>
    /// <param name="scheduleId">対象スケジュール。</param>
    /// <param name="idempotencyKey"><c>X-Idempotency-Key</c>。空なら呼び出しごとに新しい実行。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>開始した実行。</returns>
    /// <exception cref="NotFoundException">無い、削除済み、または他テナント。</exception>
    /// <exception cref="ApiValidationException"><c>enabled</c> が false、または run-as が使えない。</exception>
    Task<ExecutionResponse> RunNowAsync(
        Guid scheduleId,
        string? idempotencyKey,
        CancellationToken cancellationToken);
}

/// <summary>overlap の許可値。</summary>
public static class ExecutionScheduleOverlapPolicies
{
    /// <summary>同一スケジュール由来の非終端があれば Start しない。</summary>
    public const string Skip = "skip";

    /// <summary>非終端があっても Start する。</summary>
    public const string Allow = "allow";
}

/// <summary>枠結果の outcome。</summary>
public static class ExecutionScheduleRunOutcomes
{
    /// <summary>Start を受理した。</summary>
    public const string Started = "started";

    /// <summary>overlap skip で Start しなかった。</summary>
    public const string SkippedOverlap = "skipped_overlap";

    /// <summary>SA / テナント / Start 失敗。</summary>
    public const string Failed = "failed";

    /// <summary>システム点検が件数を残せた。</summary>
    public const string Completed = "completed";
}

/// <summary>POST /v1/schedules の本文。</summary>
public sealed class CreateExecutionScheduleRequest : IValidatableObject
{
    /// <summary>テナント内表示名（ASCII ラベル 1〜128）。</summary>
    [Required(ErrorMessage = "name is required")]
    [NotWhitespace(ErrorMessage = "name is required")]
    public string Name { get; set; } = "";

    /// <summary>対象定義（display または UUID）。</summary>
    [Required(ErrorMessage = "definitionId is required")]
    [NotWhitespace(ErrorMessage = "definitionId is required")]
    public string DefinitionId { get; set; } = "";

    /// <summary>ピンする版番号。省略時は発火時 latest。</summary>
    [JsonPropertyName("definitionVersion")]
    public int? DefinitionVersion { get; set; }

    /// <summary>ピンする版 UUID（definitionVersion より優先）。</summary>
    [JsonPropertyName("definitionVersionId")]
    public Guid? DefinitionVersionId { get; set; }

    /// <summary>5 フィールド cron。</summary>
    [Required(ErrorMessage = "cronExpression is required")]
    [NotWhitespace(ErrorMessage = "cronExpression is required")]
    public string CronExpression { get; set; } = "";

    /// <summary>IANA タイムゾーン。省略して UTC にしない。</summary>
    [Required(ErrorMessage = "timeZone is required")]
    [NotWhitespace(ErrorMessage = "timeZone is required")]
    public string TimeZone { get; set; } = "";

    /// <summary><c>skip</c> または <c>allow</c>。省略時 skip。</summary>
    public string? OverlapPolicy { get; set; }

    /// <summary>run-as ServiceAccount の Principal ID。</summary>
    [Required(ErrorMessage = "runAsPrincipalId is required")]
    public Guid RunAsPrincipalId { get; set; }

    /// <summary>Start に渡す input（任意）。</summary>
    public JsonElement? Input { get; set; }

    /// <summary>Dispatcher が発火するか。省略時 true。</summary>
    public bool Enabled { get; set; } = true;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var trimmedName = Name.Trim();
        if (!AsciiLabelConstraints.IsValid(trimmedName, AsciiLabelConstraints.DefaultMaxLength))
            yield return new ValidationResult(AsciiLabelConstraints.FormatErrorMessage, [nameof(Name)]);

        if (OverlapPolicy is { } overlap &&
            overlap.Length > 0 &&
            !IsSupportedOverlap(overlap))
        {
            yield return new ValidationResult(
                "overlapPolicy must be skip or allow.",
                [nameof(OverlapPolicy)]);
        }
    }

    private static bool IsSupportedOverlap(string overlap) =>
        overlap.Equals(ExecutionScheduleOverlapPolicies.Skip, StringComparison.Ordinal) ||
        overlap.Equals(ExecutionScheduleOverlapPolicies.Allow, StringComparison.Ordinal);
}

/// <summary>PATCH /v1/schedules/{id} の本文。省略した項目は維持する。</summary>
public sealed class UpdateExecutionScheduleRequest : IValidatableObject
{
    /// <summary>テナント内表示名。</summary>
    public string? Name { get; set; }

    /// <summary>対象定義（display または UUID）。</summary>
    public string? DefinitionId { get; set; }

    /// <summary>ピンする版番号。</summary>
    [JsonPropertyName("definitionVersion")]
    public int? DefinitionVersion { get; set; }

    /// <summary>ピンする版 UUID。</summary>
    [JsonPropertyName("definitionVersionId")]
    public Guid? DefinitionVersionId { get; set; }

    /// <summary>5 フィールド cron。</summary>
    public string? CronExpression { get; set; }

    /// <summary>IANA タイムゾーン。</summary>
    public string? TimeZone { get; set; }

    /// <summary><c>skip</c> または <c>allow</c>。</summary>
    public string? OverlapPolicy { get; set; }

    /// <summary>run-as ServiceAccount の Principal ID。</summary>
    public Guid? RunAsPrincipalId { get; set; }

    /// <summary>Start に渡す input。指定時のみ上書きする。</summary>
    public JsonElement? Input { get; set; }

    /// <summary>Dispatcher が発火するか。</summary>
    public bool? Enabled { get; set; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name is { } name)
        {
            var trimmedName = name.Trim();
            if (!AsciiLabelConstraints.IsValid(trimmedName, AsciiLabelConstraints.DefaultMaxLength))
                yield return new ValidationResult(AsciiLabelConstraints.FormatErrorMessage, [nameof(Name)]);
        }

        if (OverlapPolicy is { } overlap &&
            overlap.Length > 0 &&
            !overlap.Equals(ExecutionScheduleOverlapPolicies.Skip, StringComparison.Ordinal) &&
            !overlap.Equals(ExecutionScheduleOverlapPolicies.Allow, StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "overlapPolicy must be skip or allow.",
                [nameof(OverlapPolicy)]);
        }
    }
}

/// <summary>一覧行。機微な <c>input</c> は含めない。</summary>
public class ExecutionScheduleListItemDto
{
    /// <summary>スケジュール UUID。</summary>
    public Guid ScheduleId { get; set; }

    /// <summary>テナント内表示名。</summary>
    public string Name { get; set; } = "";

    /// <summary>対象定義。</summary>
    public Guid DefinitionId { get; set; }

    /// <summary>ピンした定義版。null なら発火時 latest。</summary>
    public Guid? DefinitionVersionId { get; set; }

    /// <summary>run-as ServiceAccount。</summary>
    public Guid RunAsPrincipalId { get; set; }

    /// <summary>5 フィールド cron。</summary>
    public string CronExpression { get; set; } = "";

    /// <summary>IANA タイムゾーン。</summary>
    public string TimeZone { get; set; } = "";

    /// <summary><c>skip</c> または <c>allow</c>。</summary>
    public string OverlapPolicy { get; set; } = "";

    /// <summary>Dispatcher が発火するか。</summary>
    public bool Enabled { get; set; }

    /// <summary>次枠（UTC）。</summary>
    public DateTime NextFireAt { get; set; }

    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>更新日時（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>単票。input を含む。</summary>
public sealed class ExecutionScheduleDetailDto : ExecutionScheduleListItemDto
{
    /// <summary>作成した Principal。</summary>
    public Guid CreatedByPrincipalId { get; set; }

    /// <summary>Start に渡す input。未設定なら null。</summary>
    public JsonElement? Input { get; set; }
}
