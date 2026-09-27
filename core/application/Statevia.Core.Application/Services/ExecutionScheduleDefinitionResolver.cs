namespace Statevia.Core.Application.Services;

/// <summary>スケジュール CRUD 向けの定義 ID / 版ピン解決。</summary>
/// <param name="displayIds">定義 display / UUID 解決。</param>
/// <param name="definitions">定義版の読み取り。</param>
/// <param name="executor">読み取りトランザクション。</param>
internal sealed class ExecutionScheduleDefinitionResolver(
    IDisplayIdService displayIds,
    IDefinitionRepository definitions,
    ICoreTransactionExecutor executor)
{
    /// <summary>display または UUID を定義 UUID に解決する。</summary>
    /// <param name="definitionIdOrDisplay">定義 ID。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>定義 UUID。</returns>
    /// <exception cref="NotFoundException">定義が無い。</exception>
    public async Task<Guid> ResolveDefinitionIdAsync(string definitionIdOrDisplay, CancellationToken cancellationToken)
    {
        var definitionId = await displayIds
            .ResolveAsync(DisplayIdResourceTypes.Definition, definitionIdOrDisplay, cancellationToken)
            .ConfigureAwait(false);
        if (definitionId is null)
            throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);
        return definitionId.Value;
    }

    /// <summary>指定があれば版をピンし、省略時は null（発火時 latest）。</summary>
    /// <param name="tenantId">テナント。</param>
    /// <param name="definitionId">対象定義。</param>
    /// <param name="definitionVersionId">版 UUID。</param>
    /// <param name="definitionVersion">版番号。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>ピンした版 UUID。未指定なら null。</returns>
    /// <exception cref="NotFoundException">版が無い、または定義と一致しない。</exception>
    public Task<Guid?> ResolvePinnedVersionIdAsync(
        Guid tenantId,
        Guid definitionId,
        Guid? definitionVersionId,
        int? definitionVersion,
        CancellationToken cancellationToken) =>
        executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                if (definitionVersionId is { } versionId)
                {
                    var byId = await definitions
                        .GetVersionForExecutionByIdAsync(uow, tenantId, versionId, innerCt)
                        .ConfigureAwait(false);
                    if (byId is null || byId.DefinitionId != definitionId)
                        throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);
                    var parent = await definitions
                        .GetLatestForApiAsync(uow, tenantId, definitionId, innerCt)
                        .ConfigureAwait(false);
                    if (parent is null)
                        throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);
                    return byId.DefinitionVersionId;
                }

                if (definitionVersion is { } versionNumber)
                {
                    var byNumber = await definitions
                        .GetVersionForApiAsync(uow, tenantId, definitionId, versionNumber, innerCt)
                        .ConfigureAwait(false);
                    if (byNumber is null)
                        throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);
                    return byNumber.DefinitionVersionId;
                }

                return (Guid?)null;
            },
            cancellationToken);
}
