using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Application.Contracts.Validation;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Controllers;

/// <summary>
/// ワークフロー定義 REST API（<c>/v1/definitions</c>）。
/// </summary>
/// <remarks>
/// 定義 YAML を受け取る作成・検証・publish は <see cref="DefinitionYamlConstraints.MaxRequestBodyBytes"/> でボディを打ち切る（超過は 413）。
/// </remarks>
[ApiController]
[Route("v1/definitions")]
public class DefinitionsController : ControllerBase
{
    private readonly IDefinitionService _definitions;

    /// <summary>
    /// <see cref="DefinitionsController"/> を生成する。
    /// </summary>
    /// <param name="definitions">定義サービス。</param>
    public DefinitionsController(
        IDefinitionService definitions)
    {
        _definitions = definitions;
    }

    /// <summary>POST /v1/definitions — 定義を登録。name + yaml + projectId を受け取り、検証・コンパイルして保存。</summary>
    /// <param name="request">要求の本文。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>201 と作成した定義。</returns>
    [HttpPost]
    [RequestSizeLimit(DefinitionYamlConstraints.MaxRequestBodyBytes)]
    [ProducesResponseType(typeof(DefinitionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<DefinitionResponse>> Create(
        [FromBody] CreateDefinitionRequest request,
        CancellationToken ct = default)
    {
        var created = await _definitions.CreateAsync(request, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(Get), new { id = created.DisplayId }, created);
    }

    /// <summary>POST /v1/definitions/validate — 定義 YAML を検証する。catalog へは保存しない。</summary>
    /// <param name="request">要求の本文。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>200 と検証結果。</returns>
    [HttpPost("validate")]
    [RequestSizeLimit(DefinitionYamlConstraints.MaxRequestBodyBytes)]
    [ProducesResponseType(typeof(ValidateDefinitionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidateDefinitionResponse>> Validate(
        [FromBody] ValidateDefinitionRequest request,
        CancellationToken ct = default)
    {
        var result = await _definitions.ValidateAsync(request, ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>PUT /v1/definitions/{id} — 定義を更新。name + yaml を受け取り、検証・コンパイルして保存。</summary>
    /// <param name="id">表示用 ID または UUID の文字列。</param>
    /// <param name="request">要求の本文。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>200 と更新した定義。</returns>
    [HttpPut("{id}")]
    [RequestSizeLimit(DefinitionYamlConstraints.MaxRequestBodyBytes)]
    [ProducesResponseType(typeof(DefinitionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DefinitionResponse>> Update(
        string id,
        [FromBody] UpdateDefinitionRequest request,
        CancellationToken ct = default)
    {
        var updated = await _definitions.UpdateAsync(id, request, ct).ConfigureAwait(false);
        return Ok(updated);
    }

    /// <summary>
    /// GET /v1/definitions — ページング一覧。<c>limit</c> は必須。
    /// <c>?limit=&amp;offset=&amp;name=&amp;projectId=&amp;sortBy=&amp;sortOrder=</c> で <see cref="PagedResult{T}"/>（name は部分一致。projectId は任意）。
    /// </summary>
    /// <param name="query">ページングと絞り込みの条件。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>200 と定義のページ。</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DefinitionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] DefinitionListQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var offset = query.Offset ?? 0;
        var pageQuery = new DefinitionListPageQuery(
            Page: new PageQuery(offset, query.Limit!.Value),
            Sort: new SortQuery(query.SortBy, query.SortOrder),
            NameContains: query.Name,
            IncludeDeleted: query.IncludeDeleted ?? false,
            ProjectId: query.ProjectId);

        var paged = await _definitions.ListPagedAsync(pageQuery, ct).ConfigureAwait(false);
        return Ok(paged);
    }

    /// <summary>GET /v1/definitions/{id} — 表示用 ID または UUID で取得。</summary>
    /// <param name="id">表示用 ID または UUID の文字列。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>200 と定義。</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DefinitionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DefinitionResponse>> Get(
        string id,
        CancellationToken ct = default)
    {
        var row = await _definitions.GetAsync(id, ct).ConfigureAwait(false);
        return Ok(row);
    }

    /// <summary>DELETE /v1/definitions/{id} — catalog から論理削除する。</summary>
    /// <param name="id">表示用 ID または UUID の文字列。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>204（本文なし）。</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        await _definitions.DeleteAsync(id, ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>POST /v1/definitions/{id}/restore — 削除済み定義を復元する。</summary>
    /// <param name="id">表示用 ID または UUID の文字列。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>200 と復元した定義。</returns>
    [HttpPost("{id}/restore")]
    [ProducesResponseType(typeof(DefinitionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DefinitionResponse>> Restore(string id, CancellationToken ct = default)
    {
        var restored = await _definitions.RestoreAsync(id, ct).ConfigureAwait(false);
        return Ok(restored);
    }
}
