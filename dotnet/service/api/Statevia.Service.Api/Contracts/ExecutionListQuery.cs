using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Statevia.Service.Api.Contracts;

/// <summary>
/// GET /v1/executions のクエリパラメータ（ページング・フィルタ・ソート）。
/// </summary>
/// <remarks>
/// <para>
/// <c>status</c> は契約の実行ステータス（<c>ACTIVE</c> / <c>COMPLETED</c> / <c>FAILED</c> / <c>CANCELED</c>）だけを受ける。
/// 投影の値（<c>Running</c> など）・<c>UNKNOWN</c>・それ以外は 422 にする。空と空白だけは絞り込まない。
/// </para>
/// <para>
/// <c>waiting</c> は待機中（自身または子孫の物理子 Execution が <c>EventWait</c> / <c>CallbackWait</c> を持つ <c>ACTIVE</c> の実行）、
/// <c>needsAttention</c> は要対応（待機中または <c>FAILED</c>。期間で区切らない）で絞る。条件はすべて AND で効く。
/// </para>
/// <para>
/// <c>sortBy</c> は <c>updatedAt</c>（既定）/ <c>displayId</c> / <c>startedAt</c>、<c>sortOrder</c> は <c>asc</c> / <c>desc</c>（既定）だけを受け、
/// それ以外は 422 にする。<c>startedFrom</c>（含む）/ <c>startedTo</c>（含まない）は ISO 8601 で <c>Z</c> かオフセットが必須。
/// </para>
/// </remarks>
public sealed class ExecutionListQuery : IValidatableObject
{
    /// <summary>ページサイズ（必須。1〜500）。</summary>
    [FromQuery(Name = "limit")]
    [Required(ErrorMessage = "limit is required")]
    [Range(1, 500, ErrorMessage = "limit must be at most 500")]
    public int? Limit { get; init; }

    /// <summary>オフセット（0 以上）。</summary>
    [FromQuery(Name = "offset")]
    [Range(0, int.MaxValue, ErrorMessage = "offset must be >= 0")]
    public int? Offset { get; init; }

    /// <summary>実行ステータスのフィルタ（契約の値。大文字小文字を区別する）。</summary>
    [FromQuery(Name = "status")]
    public string? Status { get; init; }

    /// <summary>定義 ID（display または UUID）。</summary>
    [FromQuery(Name = "definitionId")]
    public string? DefinitionId { get; init; }

    /// <summary>指定時はその Project の実行だけ。未所属は 0 件。</summary>
    [FromQuery(Name = "projectId")]
    public Guid? ProjectId { get; init; }

    /// <summary>表示 ID 部分一致または execution UUID 完全一致。</summary>
    [FromQuery(Name = "name")]
    public string? Name { get; init; }

    /// <summary>ソート列（<c>updatedAt</c> / <c>displayId</c> / <c>startedAt</c>）。</summary>
    [FromQuery(Name = "sortBy")]
    public string? SortBy { get; init; }

    /// <summary>ソート順（<c>asc</c> / <c>desc</c>）。</summary>
    [FromQuery(Name = "sortOrder")]
    public string? SortOrder { get; init; }

    /// <summary>待機中だけ（<c>true</c>）か、待機中でないものだけ（<c>false</c>）。省略時は絞らない。</summary>
    [FromQuery(Name = "waiting")]
    public bool? Waiting { get; init; }

    /// <summary>要対応（待機中または <c>FAILED</c>）だけ（<c>true</c>）か、それ以外だけ（<c>false</c>）。省略時は絞らない。</summary>
    [FromQuery(Name = "needsAttention")]
    public bool? NeedsAttention { get; init; }

    /// <summary>開始日時の下限（含む。ISO 8601、<c>Z</c> かオフセットが必須）。</summary>
    [FromQuery(Name = "startedFrom")]
    public string? StartedFrom { get; init; }

    /// <summary>開始日時の上限（含まない。ISO 8601、<c>Z</c> かオフセットが必須）。</summary>
    [FromQuery(Name = "startedTo")]
    public string? StartedTo { get; init; }

    /// <summary><c>status</c> を投影の値に戻す。検証を通った後に呼ぶ。</summary>
    /// <returns>投影の値。<c>status</c> が空のときは <see langword="null"/>（絞り込まない）。</returns>
    public string? ResolveProjectionStatusFilter() =>
        !string.IsNullOrWhiteSpace(Status) && ExecutionStatusMapper.TryToProjection(Status, out var projectionStatus)
            ? projectionStatus
            : null;

    /// <summary>検証済みのクエリをリポジトリのページクエリにする。</summary>
    /// <param name="definitionIdFilter">解決済みの定義 UUID。</param>
    /// <returns>ページクエリ。</returns>
    public ExecutionListPageQuery ToPageQuery(Guid? definitionIdFilter) =>
        new(
            Page: new PageQuery(Offset ?? 0, Limit!.Value),
            Sort: new SortQuery(ListQueryValidation.Normalize(SortBy), ListQueryValidation.Normalize(SortOrder)),
            StatusFilter: ResolveProjectionStatusFilter(),
            DefinitionIdFilter: definitionIdFilter,
            NameContains: ListQueryValidation.Normalize(Name),
            ProjectId: ProjectId is Guid projectId && projectId != Guid.Empty ? projectId : null,
            StartedFrom: ListQueryValidation.ResolveUtc(StartedFrom),
            StartedTo: ListQueryValidation.ResolveUtc(StartedTo),
            Waiting: Waiting,
            NeedsAttention: NeedsAttention);

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Status) && !ExecutionStatusMapper.TryToProjection(Status, out _))
        {
            yield return new ValidationResult(
                $"status must be one of {string.Join(", ", ExecutionStatusMapper.FilterableContractStatuses)}",
                [nameof(Status)]);
        }

        var otherErrors = ListQueryValidation
            .ValidateSort(SortBy, ExecutionListSortKeys.All, SortOrder, nameof(SortBy), nameof(SortOrder))
            .Concat(ListQueryValidation.ValidateStartedRange(StartedFrom, StartedTo, nameof(StartedFrom), nameof(StartedTo)));
        foreach (var error in otherErrors)
            yield return error;
    }
}
