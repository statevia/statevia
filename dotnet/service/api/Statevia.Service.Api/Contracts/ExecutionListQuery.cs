using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Statevia.Service.Api.Contracts;

/// <summary>
/// GET /v1/executions のクエリパラメータ（ページング・フィルタ・ソート）。
/// </summary>
/// <remarks>
/// <c>status</c> は契約の実行ステータス（<c>ACTIVE</c> / <c>COMPLETED</c> / <c>FAILED</c> / <c>CANCELED</c>）だけを受ける。
/// 投影の値（<c>Running</c> など）・<c>UNKNOWN</c>・それ以外は 422 にする。空と空白だけは絞り込まない。
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

    /// <summary>ソート列。</summary>
    [FromQuery(Name = "sortBy")]
    public string? SortBy { get; init; }

    /// <summary>ソート順。</summary>
    [FromQuery(Name = "sortOrder")]
    public string? SortOrder { get; init; }

    /// <summary><c>status</c> を投影の値に戻す。検証を通った後に呼ぶ。</summary>
    /// <returns>投影の値。<c>status</c> が空のときは <see langword="null"/>（絞り込まない）。</returns>
    public string? ResolveProjectionStatusFilter() =>
        !string.IsNullOrWhiteSpace(Status) && ExecutionStatusMapper.TryToProjection(Status, out var projectionStatus)
            ? projectionStatus
            : null;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Status) && !ExecutionStatusMapper.TryToProjection(Status, out _))
        {
            yield return new ValidationResult(
                $"status must be one of {string.Join(", ", ExecutionStatusMapper.FilterableContractStatuses)}",
                [nameof(Status)]);
        }
    }
}
