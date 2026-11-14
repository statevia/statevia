using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Statevia.Service.Api.Contracts;

/// <summary>
/// GET /v1/definitions のクエリパラメータ（ページング・フィルタ・ソート）。
/// </summary>
/// <remarks>
/// <c>sortBy</c> は <c>createdAt</c>（既定）/ <c>name</c> / <c>updatedAt</c>、<c>sortOrder</c> は <c>asc</c> / <c>desc</c>（既定）だけを受け、
/// それ以外は 422 にする（大文字小文字を区別する）。空と空白だけは省略と同じ。
/// </remarks>
public sealed class DefinitionListQuery : IValidatableObject
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

    /// <summary>定義名の部分一致。</summary>
    [FromQuery(Name = "name")]
    public string? Name { get; init; }

    /// <summary>ソート列（<c>createdAt</c> / <c>name</c> / <c>updatedAt</c>）。</summary>
    [FromQuery(Name = "sortBy")]
    public string? SortBy { get; init; }

    /// <summary>ソート順（<c>asc</c> / <c>desc</c>）。</summary>
    [FromQuery(Name = "sortOrder")]
    public string? SortOrder { get; init; }

    /// <summary>削除済み定義を一覧に含める（省略時は false）。</summary>
    [FromQuery(Name = "includeDeleted")]
    public bool? IncludeDeleted { get; init; }

    /// <summary>所属 Project で絞る。省略時は所属の和。</summary>
    [FromQuery(Name = "projectId")]
    public Guid? ProjectId { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        ListQueryValidation.ValidateSort(SortBy, DefinitionListSortKeys.All, SortOrder, nameof(SortBy), nameof(SortOrder));
}
