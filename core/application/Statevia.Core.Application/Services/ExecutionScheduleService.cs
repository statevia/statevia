using System.Text.Json;
using Statevia.Core.Application.Scheduling;

namespace Statevia.Core.Application.Services;

/// <summary>定期実行スケジュールの CRUD。定義 YAML は変更せず、SA も発行しない。</summary>
/// <remarks>
/// <para>書き込みは <c>executions.write</c> と対象定義の project executor。読み取りは <c>executions.read</c>。</para>
/// <para>他テナントの ID は 404。run-as は同一テナントの Active な ServiceAccount のみ。</para>
/// </remarks>
/// <param name="schedules">スケジュール行。</param>
/// <param name="authorization">実行認可。</param>
/// <param name="definitions">定義 ID / 版ピン解決。</param>
/// <param name="principals">run-as 検証。</param>
/// <param name="tenantContext">呼び出しテナント。</param>
/// <param name="ids">スケジュール ID 生成。</param>
/// <param name="dispatch">手動実行の Start。cron の next は更新しない。</param>
internal sealed class ExecutionScheduleService(
    IExecutionScheduleRepository schedules,
    ExecutionAuthorizationGuard authorization,
    ExecutionScheduleDefinitionResolver definitions,
    IPrincipalDataAccess principals,
    ITenantContextAccessor tenantContext,
    IIdGenerator ids,
    IExecutionScheduleDispatchService dispatch) : IExecutionScheduleService
{
    private const string ScheduleNotFound = "Schedule not found";
    private const string RunAsInvalid = "runAsPrincipalId must be an active ServiceAccount in the tenant.";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionScheduleListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        await authorization.EnsureExecutionsReadAsync(cancellationToken).ConfigureAwait(false);
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await schedules.ListAsync(cancellationToken).ConfigureAwait(false);
        return rows
            .Where(row => row.TenantId == tenantId)
            .Select(ToListItem)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ExecutionScheduleDetailDto> GetAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        await authorization.EnsureExecutionsReadAsync(cancellationToken).ConfigureAwait(false);
        var row = await LoadOwnedOrNotFoundAsync(scheduleId, cancellationToken).ConfigureAwait(false);
        return ToDetail(row);
    }

    /// <inheritdoc />
    public async Task<ExecutionScheduleDetailDto> CreateAsync(
        CreateExecutionScheduleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await authorization.EnsureExecutionsWriteAsync(cancellationToken).ConfigureAwait(false);

        var tenantId = tenantContext.GetRequiredTenantId();
        var callerPrincipalId = RequireCallerPrincipalId();
        var name = request.Name.Trim();
        var cron = request.CronExpression.Trim();
        var timeZone = request.TimeZone.Trim();
        var overlap = NormalizeOverlapPolicy(request.OverlapPolicy);
        var definitionId = await definitions.ResolveDefinitionIdAsync(request.DefinitionId, cancellationToken).ConfigureAwait(false);
        await authorization.EnsureCanExecuteOnDefinitionAsync(tenantId, definitionId, cancellationToken)
            .ConfigureAwait(false);
        var versionId = await definitions.ResolvePinnedVersionIdAsync(
                tenantId,
                definitionId,
                request.DefinitionVersionId,
                request.DefinitionVersion,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureActiveServiceAccountAsync(tenantId, request.RunAsPrincipalId, cancellationToken)
            .ConfigureAwait(false);
        await authorization.EnsureResourceGrantForPrincipalAsync(
                tenantId,
                request.RunAsPrincipalId,
                definitionId,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureUniqueNameAsync(tenantId, name, excludingScheduleId: null, cancellationToken)
            .ConfigureAwait(false);
        var nextFireAt = ComputeNextFireAt(cron, timeZone);
        var now = DateTime.UtcNow;
        var row = new ExecutionScheduleRow
        {
            ScheduleId = ids.NewSequentialGuid(),
            TenantId = tenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = versionId,
            RunAsPrincipalId = request.RunAsPrincipalId,
            CreatedByPrincipalId = callerPrincipalId,
            Name = name,
            CronExpression = cron,
            TimeZone = timeZone,
            OverlapPolicy = overlap,
            InputJson = SerializeInput(request.Input),
            Enabled = request.Enabled,
            NextFireAt = nextFireAt,
            CreatedAt = now,
            UpdatedAt = now
        };
        await schedules.AddAsync(row, cancellationToken).ConfigureAwait(false);
        return ToDetail(row);
    }

    /// <inheritdoc />
    public async Task<ExecutionScheduleDetailDto> UpdateAsync(
        Guid scheduleId,
        UpdateExecutionScheduleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await authorization.EnsureExecutionsWriteAsync(cancellationToken).ConfigureAwait(false);
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await LoadOwnedOrNotFoundAsync(scheduleId, cancellationToken).ConfigureAwait(false);

        if (request.Name is { } nameRaw)
        {
            var name = nameRaw.Trim();
            await EnsureUniqueNameAsync(tenantId, name, row.ScheduleId, cancellationToken).ConfigureAwait(false);
            row.Name = name;
        }

        if (request.DefinitionId is { } definitionRaw)
            row.DefinitionId = await definitions.ResolveDefinitionIdAsync(definitionRaw, cancellationToken).ConfigureAwait(false);

        if (request.DefinitionVersionId is { } || request.DefinitionVersion is { })
        {
            row.DefinitionVersionId = await definitions.ResolvePinnedVersionIdAsync(
                    tenantId,
                    row.RequireDefinitionId(),
                    request.DefinitionVersionId,
                    request.DefinitionVersion,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await authorization.EnsureCanExecuteOnDefinitionAsync(tenantId, row.RequireDefinitionId(), cancellationToken)
            .ConfigureAwait(false);

        if (request.RunAsPrincipalId is { } runAs)
        {
            await EnsureActiveServiceAccountAsync(tenantId, runAs, cancellationToken).ConfigureAwait(false);
            row.RunAsPrincipalId = runAs;
        }

        await authorization.EnsureResourceGrantForPrincipalAsync(
                tenantId,
                row.RequireRunAsPrincipalId(),
                row.RequireDefinitionId(),
                cancellationToken)
            .ConfigureAwait(false);

        if (request.OverlapPolicy is { } overlapRaw && overlapRaw.Length > 0)
            row.OverlapPolicy = NormalizeOverlapPolicy(overlapRaw);

        var cronChanged = false;
        if (request.CronExpression is { } cronRaw)
        {
            row.CronExpression = cronRaw.Trim();
            cronChanged = true;
        }

        if (request.TimeZone is { } timeZoneRaw)
        {
            row.TimeZone = timeZoneRaw.Trim();
            cronChanged = true;
        }

        if (request.Input is { } input)
            row.InputJson = SerializeInput(input);

        if (request.Enabled is { } enabled)
            row.Enabled = enabled;

        if (cronChanged)
            row.NextFireAt = ComputeNextFireAt(row.CronExpression, row.TimeZone);

        row.UpdatedAt = DateTime.UtcNow;
        await schedules.UpdateAsync(row, cancellationToken).ConfigureAwait(false);
        return ToDetail(row);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        await authorization.EnsureExecutionsWriteAsync(cancellationToken).ConfigureAwait(false);
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await LoadOwnedOrNotFoundAsync(scheduleId, cancellationToken).ConfigureAwait(false);
        await authorization.EnsureCanExecuteOnDefinitionAsync(tenantId, row.RequireDefinitionId(), cancellationToken)
            .ConfigureAwait(false);
        var now = DateTime.UtcNow;
        row.DeletedAt = now;
        row.UpdatedAt = now;
        await schedules.UpdateAsync(row, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ExecutionResponse> RunNowAsync(
        Guid scheduleId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        await authorization.EnsureExecutionsWriteAsync(cancellationToken).ConfigureAwait(false);
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await LoadOwnedOrNotFoundAsync(scheduleId, cancellationToken).ConfigureAwait(false);
        await authorization.EnsureCanExecuteOnDefinitionAsync(tenantId, row.RequireDefinitionId(), cancellationToken)
            .ConfigureAwait(false);
        if (!row.Enabled)
            throw new ApiValidationException("schedule is disabled.", new { field = "enabled" });

        return await dispatch.RunManuallyAsync(scheduleId, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ExecutionScheduleRow> LoadOwnedOrNotFoundAsync(
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await schedules.GetByIdAsync(scheduleId, cancellationToken).ConfigureAwait(false);
        if (row is null || row.TenantId != tenantId)
            throw new NotFoundException(ScheduleNotFound);
        return row;
    }

    private Guid RequireCallerPrincipalId() =>
        tenantContext.PrincipalId
        ?? throw new UnauthorizedException("Caller principal is required.");

    private async Task EnsureUniqueNameAsync(
        Guid tenantId,
        string name,
        Guid? excludingScheduleId,
        CancellationToken cancellationToken)
    {
        var existing = await schedules.GetByNameAsync(tenantId, name, cancellationToken).ConfigureAwait(false);
        if (existing is null || existing.ScheduleId == excludingScheduleId)
            return;

        throw new ApiValidationException(
            "name must be unique in the tenant.",
            new { field = "name" });
    }

    private async Task EnsureActiveServiceAccountAsync(
        Guid tenantId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        var principal = await principals.FindPrincipalAsync(principalId, cancellationToken).ConfigureAwait(false);
        var usable = principal is not null
            && principal.TenantId == tenantId
            && principal.PrincipalType == PrincipalType.ServiceAccount
            && principal.IsActive
            && principal.DisabledAt is null
            && principal.DeletedAt is null;
        if (!usable)
            throw new ApiValidationException(RunAsInvalid, new { field = "runAsPrincipalId" });
    }

    private static string NormalizeOverlapPolicy(string? overlap)
    {
        if (string.IsNullOrWhiteSpace(overlap))
            return ExecutionScheduleOverlapPolicies.Skip;

        var trimmed = overlap.Trim();
        if (trimmed.Equals(ExecutionScheduleOverlapPolicies.Skip, StringComparison.Ordinal) ||
            trimmed.Equals(ExecutionScheduleOverlapPolicies.Allow, StringComparison.Ordinal))
            return trimmed;

        throw new ApiValidationException(
            "overlapPolicy must be skip or allow.",
            new { field = "overlapPolicy" });
    }

    private static DateTime ComputeNextFireAt(string cronExpression, string timeZone)
    {
        try
        {
            return CronNextFireCalculator.NextStrictlyAfter(cronExpression, timeZone, DateTime.UtcNow);
        }
        catch (ArgumentException ex) when (string.Equals(ex.ParamName, "cronExpression", StringComparison.Ordinal))
        {
            throw new ApiValidationException(
                "cronExpression must be a 5-field cron.",
                new { field = "cronExpression" },
                ex);
        }
        catch (ArgumentException ex) when (string.Equals(ex.ParamName, "timeZoneId", StringComparison.Ordinal))
        {
            throw new ApiValidationException(
                "timeZone must be a valid IANA identifier.",
                new { field = "timeZone" },
                ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new ApiValidationException(
                "cronExpression has no next occurrence.",
                new { field = "cronExpression" },
                ex);
        }
    }

    private static string? SerializeInput(JsonElement? input)
    {
        if (input is not { } json || json.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;
        return json.GetRawText();
    }

    private static JsonElement? DeserializeInput(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static ExecutionScheduleListItemDto ToListItem(ExecutionScheduleRow row) =>
        new()
        {
            ScheduleId = row.ScheduleId,
            Name = row.Name,
            DefinitionId = row.RequireDefinitionId(),
            DefinitionVersionId = row.DefinitionVersionId,
            RunAsPrincipalId = row.RequireRunAsPrincipalId(),
            CronExpression = row.CronExpression,
            TimeZone = row.TimeZone,
            OverlapPolicy = row.OverlapPolicy,
            Enabled = row.Enabled,
            NextFireAt = row.NextFireAt,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };

    private static ExecutionScheduleDetailDto ToDetail(ExecutionScheduleRow row) =>
        new()
        {
            ScheduleId = row.ScheduleId,
            Name = row.Name,
            DefinitionId = row.RequireDefinitionId(),
            DefinitionVersionId = row.DefinitionVersionId,
            RunAsPrincipalId = row.RequireRunAsPrincipalId(),
            CreatedByPrincipalId = row.CreatedByPrincipalId,
            CronExpression = row.CronExpression,
            TimeZone = row.TimeZone,
            OverlapPolicy = row.OverlapPolicy,
            Enabled = row.Enabled,
            NextFireAt = row.NextFireAt,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
            Input = DeserializeInput(row.InputJson)
        };
}
