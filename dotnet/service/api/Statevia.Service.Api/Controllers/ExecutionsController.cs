using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Application.Contracts.Validation;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Services;
using System.ComponentModel.DataAnnotations;

namespace Statevia.Service.Api.Controllers;

/// <summary>
/// 実行 REST API（<c>/v1/executions</c>）。
/// </summary>
[ApiController]
[Route("v1/executions")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S6960:Controllers should not have mixed responsibilities",
    Justification = "実行 CRUD と SSE は同一リソース境界のため当面は単一コントローラで維持する。")]
public class ExecutionsController : ControllerBase
{
    private const string IdempotencyKeyHeaderName = "X-Idempotency-Key";

    private readonly IExecutionService _executions;
    private readonly ExecutionStreamService _stream;
    private readonly IDisplayIdService _displayIds;

    /// <summary>
    /// <see cref="ExecutionsController"/> を生成する。
    /// </summary>
    /// <param name="executions">実行サービス。</param>
    /// <param name="stream">SSE 用ストリームサービス。</param>
    /// <param name="displayIds">表示 ID 解決。</param>
    public ExecutionsController(
        IExecutionService executions,
        ExecutionStreamService stream,
        IDisplayIdService displayIds)
    {
        _executions = executions;
        _stream = stream;
        _displayIds = displayIds;
    }

    /// <summary>POST /v1/executions — definitionId で定義を取得し、Engine.Start を呼ぶ。display_id と resource_id を返す。</summary>
    /// <param name="request">要求の本文。</param>
    /// <param name="idempotencyKey"><c>X-Idempotency-Key</c> の値。無ければ冪等にしない。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>201 と作成したExecution。</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ExecutionResponse>> Create(
        [FromBody] StartExecutionRequest request,
        [FromHeader(Name = IdempotencyKeyHeaderName)]
        [RegularExpression(PrintableAsciiConstraints.AllowedPattern, ErrorMessage = PrintableAsciiConstraints.FormatErrorMessage)]
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var created = await _executions.StartAsync(
            request,
            idempotencyKey,
            new CommandRequestContext(Request.Method, Request.Path.Value ?? string.Empty),
            ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(Get), new { executionId = created.DisplayId }, created);
    }

    /// <summary>
    /// GET /v1/executions — ページング一覧。<c>limit</c> は必須。
    /// <c>?limit=&amp;offset=&amp;status=&amp;definitionId=&amp;name=&amp;projectId=&amp;waiting=&amp;needsAttention=&amp;startedFrom=&amp;startedTo=&amp;sortBy=&amp;sortOrder=</c> で <see cref="PagedResult{T}"/>。
    /// <c>definitionId</c> は定義の display / UUID。 <c>name</c> は execution の <c>displayId</c> 部分一致、または execution の UUID 完全一致で絞り込み。
    /// 応答の <c>items</c> と <c>totalCount</c> は、<c>execution_branches.execution_id</c> に無いルートのみ。各要素は待機中かどうか（<c>waiting</c>）を持つ。
    /// </summary>
    /// <param name="query">ページングと絞り込みの条件。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>200 とExecution のページ。</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ExecutionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] ExecutionListQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var offset = query.Offset ?? 0;

        Guid? definitionIdFilter = null;
        if (!string.IsNullOrWhiteSpace(query.DefinitionId))
        {
            definitionIdFilter = await _displayIds.ResolveAsync(
                DisplayIdResourceTypes.Definition, query.DefinitionId, ct).ConfigureAwait(false);
            if (definitionIdFilter is null)
                return Ok(new PagedResult<ExecutionResponse> { Items = [], TotalCount = 0, Offset = offset, Limit = query.Limit!.Value, HasMore = false });
        }

        var paged = await _executions.ListPagedAsync(query.ToPageQuery(definitionIdFilter), ct).ConfigureAwait(false);
        return Ok(paged);
    }

    /// <summary>
    /// GET /v1/executions/counts — ルートの実行の件数を、状態ごと・待機中・要対応で返す。
    /// <c>?definitionId=&amp;projectId=&amp;name=&amp;startedFrom=&amp;startedTo=</c> は一覧と同じ意味で絞る。権限は一覧と同じ <c>executions.read</c>。
    /// </summary>
    /// <remarks>文字列の区間 <c>counts</c> は <c>{executionId}</c> のルートより優先される。</remarks>
    /// <param name="query">絞り込みの条件。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>200 と件数。</returns>
    [HttpGet("counts")]
    [ProducesResponseType(typeof(ExecutionCountsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExecutionCountsResponse>> Counts(
        [FromQuery] ExecutionCountQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        Guid? definitionIdFilter = null;
        if (!string.IsNullOrWhiteSpace(query.DefinitionId))
        {
            definitionIdFilter = await _displayIds.ResolveAsync(
                DisplayIdResourceTypes.Definition, query.DefinitionId, ct).ConfigureAwait(false);
            if (definitionIdFilter is null)
                return Ok(EmptyCounts());
        }

        var counts = await _executions.CountAsync(query.ToFilter(definitionIdFilter), ct).ConfigureAwait(false);
        return Ok(counts);
    }

    /// <summary>GET /v1/executions/{executionId} — 一覧と同一の <see cref="ExecutionResponse"/>（UI ExecutionDTO 向け）。X-Tenant-Id でスコープ。</summary>
    /// <param name="executionId">Execution の表示用 ID または UUID の文字列。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>200 とExecution。</returns>
    [HttpGet("{executionId}")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExecutionResponse>> Get(
        string executionId,
        CancellationToken ct = default)
    {
        var model = await _executions.GetExecutionResponseAsync(executionId, ct).ConfigureAwait(false);
        return Ok(model);
    }

    /// <summary>GET /v1/executions/{executionId}/graph — execution_graph_snapshots から取得。X-Tenant-Id でスコープ。</summary>
    /// <param name="executionId">Execution の表示用 ID または UUID の文字列。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>200 と実行グラフの JSON。</returns>
    [HttpGet("{executionId}/graph")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<string>> GetGraph(
        string executionId,
        CancellationToken ct = default)
    {
        var graphJson = await _executions.GetGraphJsonAsync(executionId, ct).ConfigureAwait(false);
        return Content(graphJson, "application/json");
    }

    /// <summary>
    /// GET /v1/executions/{executionId}/waits — 未完了 Wait の再開キー。正本は graph スナップショット。X-Tenant-Id でスコープ。
    /// </summary>
    /// <remarks>機微 IO 方針: <c>input</c> / <c>output</c> は返さない。権限は既存 executions GET と同じ <c>executions.read</c>。</remarks>
    /// <param name="executionId">Execution の表示用 ID または UUID の文字列。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>200 と待機中の Wait の一覧。</returns>
    [HttpGet("{executionId}/waits")]
    [ProducesResponseType(typeof(ExecutionWaitsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExecutionWaitsResponse>> GetWaits(
        string executionId,
        CancellationToken ct = default)
    {
        var waits = await _executions.GetExecutionWaitsAsync(executionId, ct).ConfigureAwait(false);
        return Ok(waits);
    }

    /// <summary>GET /v1/executions/{executionId}/state?atSeq= — UI 用 ExecutionView（リプレイは現状スナップショット近似）。</summary>
    /// <param name="executionId">Execution の表示用 ID または UUID の文字列。</param>
    /// <param name="query">ページングと絞り込みの条件。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>200 と実行ビュー。</returns>
    [HttpGet("{executionId}/state")]
    [ProducesResponseType(typeof(ExecutionViewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExecutionViewDto>> GetState(
        string executionId,
        [FromQuery] ExecutionStateQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var view = await _executions.GetExecutionViewAtSeqAsync(executionId, query.AtSeq, ct).ConfigureAwait(false);
        return Ok(view);
    }

    /// <summary>GET /v1/executions/{executionId}/events — event_store 由来のタイムライン（limit / afterSeq）。</summary>
    /// <param name="executionId">Execution の表示用 ID または UUID の文字列。</param>
    /// <param name="query">ページングと絞り込みの条件。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>200 とタイムラインのイベント。</returns>
    [HttpGet("{executionId}/events")]
    [ProducesResponseType(typeof(ExecutionEventsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExecutionEventsResponseDto>> GetEvents(
        string executionId,
        [FromQuery] ExecutionEventsQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var res = await _executions.ListEventsAsync(executionId, query.AfterSeq, query.Limit, ct).ConfigureAwait(false);
        return Ok(res);
    }

    /// <summary>GET /v1/executions/{executionId}/stream — SSE（グラフ JSON の変化を GraphUpdated として送出）。</summary>
    /// <param name="executionId">Execution の表示用 ID または UUID の文字列。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>Server-Sent Events の書き込みが終わると完了するタスク。</returns>
    [HttpGet("{executionId}/stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task GetStream(
        string executionId,
        CancellationToken ct = default)
    {
        await _stream.WriteStreamAsync(Response, executionId, ct).ConfigureAwait(false);
    }

    /// <summary>POST /v1/executions/{executionId}/cancel — Engine.CancelAsync を呼び、projection を更新。X-Idempotency-Key で冪等。X-Tenant-Id でスコープ。</summary>
    /// <param name="executionId">Execution の表示用 ID または UUID の文字列。</param>
    /// <param name="idempotencyKey"><c>X-Idempotency-Key</c> の値。無ければ冪等にしない。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>204（本文なし）。</returns>
    [HttpPost("{executionId}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Cancel(
        string executionId,
        [FromHeader(Name = IdempotencyKeyHeaderName)]
        [RegularExpression(PrintableAsciiConstraints.AllowedPattern, ErrorMessage = PrintableAsciiConstraints.FormatErrorMessage)]
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var resolvedIdempotencyKey = idempotencyKey;
        await _executions.CancelAsync(
            executionId,
            resolvedIdempotencyKey,
            new CommandRequestContext(Request.Method, Request.Path.Value ?? string.Empty),
            ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>POST /v1/executions/{executionId}/nodes/{nodeId}/resume — body: { "resumeKey": "..." }。PublishEvent と同様。X-Idempotency-Key で冪等。</summary>
    /// <param name="executionId">Execution の表示用 ID または UUID の文字列。</param>
    /// <param name="nodeId">Wait ノード ID。</param>
    /// <param name="body">要求の本文。</param>
    /// <param name="idempotencyKey"><c>X-Idempotency-Key</c> の値。無ければ冪等にしない。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>204（本文なし）。</returns>
    [HttpPost("{executionId}/nodes/{nodeId}/resume")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public async Task<ActionResult> ResumeNode(
        string executionId,
        [Required(ErrorMessage = "nodeId is required")]
        [NotWhitespace(ErrorMessage = "nodeId is required")]
        string nodeId,
        [FromBody] ResumeNodeRequest body,
        [FromHeader(Name = IdempotencyKeyHeaderName)]
        [RegularExpression(PrintableAsciiConstraints.AllowedPattern, ErrorMessage = PrintableAsciiConstraints.FormatErrorMessage)]
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        var resolvedIdempotencyKey = idempotencyKey;
        await _executions.ResumeNodeAsync(
            executionId,
            nodeId,
            body.ResumeKey,
            resolvedIdempotencyKey,
            new CommandRequestContext(Request.Method, Request.Path.Value ?? string.Empty),
            ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>POST /v1/executions/{executionId}/events — body: { "name": "Approve" }。Engine.PublishEvent(executionId, eventName)。X-Idempotency-Key で冪等。X-Tenant-Id でスコープ。</summary>
    /// <param name="executionId">Execution の表示用 ID または UUID の文字列。</param>
    /// <param name="body">要求の本文。</param>
    /// <param name="idempotencyKey"><c>X-Idempotency-Key</c> の値。無ければ冪等にしない。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>204（本文なし）。</returns>
    [HttpPost("{executionId}/events")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> PublishEvent(
        string executionId,
        [FromBody] PublishEventRequest body,
        [FromHeader(Name = IdempotencyKeyHeaderName)]
        [RegularExpression(PrintableAsciiConstraints.AllowedPattern, ErrorMessage = PrintableAsciiConstraints.FormatErrorMessage)]
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(body);

        var resolvedIdempotencyKey = idempotencyKey;
        await _executions.PublishEventAsync(
            executionId,
            body.Name,
            resolvedIdempotencyKey,
            new CommandRequestContext(Request.Method, Request.Path.Value ?? string.Empty),
            ct).ConfigureAwait(false);
        return NoContent();
    }

    private static ExecutionCountsResponse EmptyCounts() =>
        new() { ByStatus = new ExecutionStatusCountsResponse() };
}
