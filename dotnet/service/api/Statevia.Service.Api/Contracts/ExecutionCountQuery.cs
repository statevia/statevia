using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Statevia.Service.Api.Contracts;

/// <summary>
/// GET /v1/executions/counts のクエリパラメータ。
/// </summary>
/// <remarks>
/// 名前と意味は <see cref="ExecutionListQuery"/> の同名の項目と同じ。状態・待機・要対応では絞らない（それらごとに数えて返すため）。
/// <c>startedFrom</c>（含む）/ <c>startedTo</c>（含まない）は ISO 8601 で <c>Z</c> かオフセットが必須。
/// </remarks>
public sealed class ExecutionCountQuery : IValidatableObject
{
    /// <summary>定義 ID（display または UUID）。</summary>
    [FromQuery(Name = "definitionId")]
    public string? DefinitionId { get; init; }

    /// <summary>指定時はその Project の実行だけ。未所属は 0 件。</summary>
    [FromQuery(Name = "projectId")]
    public Guid? ProjectId { get; init; }

    /// <summary>表示 ID 部分一致または execution UUID 完全一致。</summary>
    [FromQuery(Name = "name")]
    public string? Name { get; init; }

    /// <summary>開始日時の下限（含む）。</summary>
    [FromQuery(Name = "startedFrom")]
    public string? StartedFrom { get; init; }

    /// <summary>開始日時の上限（含まない）。</summary>
    [FromQuery(Name = "startedTo")]
    public string? StartedTo { get; init; }

    /// <summary>検証済みのクエリを件数の絞り込みにする。</summary>
    /// <param name="definitionIdFilter">解決済みの定義 UUID。</param>
    /// <returns>件数の絞り込み。</returns>
    public ExecutionCountFilter ToFilter(Guid? definitionIdFilter) =>
        new(
            DefinitionIdFilter: definitionIdFilter,
            NameContains: ListQueryValidation.Normalize(Name),
            ProjectId: ProjectId is Guid projectId && projectId != Guid.Empty ? projectId : null,
            StartedFrom: ListQueryValidation.ResolveUtc(StartedFrom),
            StartedTo: ListQueryValidation.ResolveUtc(StartedTo));

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        ListQueryValidation.ValidateStartedRange(StartedFrom, StartedTo, nameof(StartedFrom), nameof(StartedTo));
}
