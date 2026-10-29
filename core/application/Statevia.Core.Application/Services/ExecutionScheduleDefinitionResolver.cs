namespace Statevia.Core.Application.Services;

/// <summary>スケジュール CRUD 向けの定義 ID / 版ピン解決と、定義に関わる発火準備検査。</summary>
/// <param name="displayIds">定義 display / UUID 解決。</param>
/// <param name="definitions">定義版の読み取り。</param>
/// <param name="executor">読み取りトランザクション。</param>
/// <param name="projects">run-as（呼び出し元ではない Principal）の実効ロール解決。</param>
internal sealed class ExecutionScheduleDefinitionResolver(
    IDisplayIdService displayIds,
    IDefinitionRepository definitions,
    ICoreTransactionExecutor executor,
    IProjectRepository projects)
{
    private const string DefinitionDeleted = "definitionId refers to a deleted definition.";

    private const string RunAsNotProjectExecutor =
        "runAsPrincipalId must be a member of the definition's project with the executor role or higher.";

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

    /// <summary>既存スケジュールの定義が論理削除されていないことを要求する。</summary>
    /// <remarks>
    /// 発火準備検査の最初の段。発火時の Start は削除済み定義を解決できず失敗するため、
    /// 有効なスケジュールの保存と手動実行の前に 422 で止める。スケジュールが指す定義そのものが無い場合も同じ扱いにする。
    /// </remarks>
    /// <param name="tenantId">テナント。</param>
    /// <param name="definitionId">スケジュールの定義。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <exception cref="ApiValidationException">定義が論理削除済みのとき。<c>field=definitionId</c>。</exception>
    public Task EnsureDefinitionNotDeletedAsync(
        Guid tenantId,
        Guid definitionId,
        CancellationToken cancellationToken) =>
        executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var projectId = await definitions
                    .ResolveProjectIdAsync(uow, tenantId, definitionId, innerCt)
                    .ConfigureAwait(false);
                if (projectId is null)
                    throw new ApiValidationException(DefinitionDeleted, new { field = "definitionId" });
            },
            cancellationToken);

    /// <summary>スケジュールの run-as が、定義の Project で executor 以上のメンバーかを要求する。</summary>
    /// <remarks>
    /// <para>呼び出し元ではなく <paramref name="runAsPrincipalId"/> を評価する。テナント管理者の免除は無い。</para>
    /// <para>発火時の Start は run-as を呼び出し元にして同じ所属を見るため、保存と手動実行の前に同じ条件で止める。</para>
    /// </remarks>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="runAsPrincipalId">run-as の Principal。</param>
    /// <param name="definitionId">未削除の論理定義 ID。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <exception cref="NotFoundException">定義が見つからないとき。</exception>
    /// <exception cref="ApiValidationException">所属が無い、または executor 未満のとき。<c>field=runAsPrincipalId</c>。</exception>
    public Task EnsureRunAsCanExecuteOnDefinitionAsync(
        Guid tenantId,
        Guid runAsPrincipalId,
        Guid definitionId,
        CancellationToken cancellationToken) =>
        executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var projectId = await definitions
                    .ResolveProjectIdAsync(uow, tenantId, definitionId, innerCt)
                    .ConfigureAwait(false);
                if (projectId is null)
                    throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

                var role = await projects
                    .ResolveEffectiveRoleAsync(uow, tenantId, runAsPrincipalId, projectId.Value, innerCt)
                    .ConfigureAwait(false);
                if (role is { } effectiveRole && ProjectAccessRolePolicy.MeetsMinimum(effectiveRole, ProjectAccessRole.Executor))
                    return;

                throw new ApiValidationException(RunAsNotProjectExecutor, new { field = "runAsPrincipalId" });
            },
            cancellationToken);

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
