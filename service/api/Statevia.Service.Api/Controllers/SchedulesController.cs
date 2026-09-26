using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Contracts.Validation;
using System.ComponentModel.DataAnnotations;

namespace Statevia.Service.Api.Controllers;

/// <summary>定期実行スケジュールの HTTP。手動 <c>POST /run</c> は cron の next を変えない。</summary>
/// <param name="schedules">スケジュールユースケース。</param>

[ApiController]
[Route("v1/schedules")]
public sealed class SchedulesController(IExecutionScheduleService schedules) : ControllerBase
{
    /// <summary>GET /v1/schedules — テナント内一覧。input は含めない。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExecutionScheduleListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExecutionScheduleListItemDto>>> List(CancellationToken ct) =>
        Ok(await schedules.ListAsync(ct).ConfigureAwait(false));

    /// <summary>GET /v1/schedules/{scheduleId} — 単票。input を含む。</summary>
    [HttpGet("{scheduleId:guid}")]
    [ProducesResponseType(typeof(ExecutionScheduleDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExecutionScheduleDetailDto>> Get(Guid scheduleId, CancellationToken ct) =>
        Ok(await schedules.GetAsync(scheduleId, ct).ConfigureAwait(false));

    /// <summary>POST /v1/schedules — 作成。定義 YAML は変えない。SA は発行しない。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExecutionScheduleDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ExecutionScheduleDetailDto>> Create(
        [FromBody] CreateExecutionScheduleRequest request,
        CancellationToken ct)
    {
        var created = await schedules.CreateAsync(request, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(Get), new { scheduleId = created.ScheduleId }, created);
    }

    /// <summary>PATCH /v1/schedules/{scheduleId} — 部分更新。</summary>
    [HttpPatch("{scheduleId:guid}")]
    [ProducesResponseType(typeof(ExecutionScheduleDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExecutionScheduleDetailDto>> Update(
        Guid scheduleId,
        [FromBody] UpdateExecutionScheduleRequest request,
        CancellationToken ct) =>
        Ok(await schedules.UpdateAsync(scheduleId, request, ct).ConfigureAwait(false));

    /// <summary>DELETE /v1/schedules/{scheduleId} — 論理削除。</summary>
    [HttpDelete("{scheduleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid scheduleId, CancellationToken ct)
    {
        await schedules.DeleteAsync(scheduleId, ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>POST /v1/schedules/{scheduleId}/run — 手動 1 回。201 で実行応答。</summary>
    [HttpPost("{scheduleId:guid}/run")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ExecutionResponse>> Run(
        Guid scheduleId,
        [FromHeader(Name = "X-Idempotency-Key")]
        [RegularExpression(PrintableAsciiConstraints.AllowedPattern, ErrorMessage = PrintableAsciiConstraints.FormatErrorMessage)]
        string? idempotencyKey,
        CancellationToken ct)
    {
        var started = await schedules.RunNowAsync(scheduleId, idempotencyKey, ct).ConfigureAwait(false);
        return Created(new Uri($"/v1/executions/{started.DisplayId}", UriKind.Relative), started);
    }
}
