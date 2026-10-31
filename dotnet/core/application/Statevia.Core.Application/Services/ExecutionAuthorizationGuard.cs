namespace Statevia.Core.Application.Services;

/// <summary>
/// 実行系コマンド／照会入口の認可チェックを横断で提供する。
/// </summary>
/// <remarks>
/// <para>各ドメインサービスの入口から呼び、認可呼び出し箇所を一覧可能にする。</para>
/// <para>定義受理時のプロジェクト実行権と、Runtime / mutation snapshot ベースの read/write を担う。</para>
/// <para>User / ServiceAccount の閲覧・開始・再開・取消では、プロジェクト権限のあとにリソース許可を評価する。System は対象外。</para>
/// <para>既存 execution のミューテーション（Cancel / Resume / イベント送信 / 復旧）も、定義の project で executor 以上を Live で要求する。Owner でも Snapshot の所属は使わない。</para>
/// </remarks>
/// <param name="runtimeAuth">Runtime 権限。</param>
/// <param name="mutationAuth">実行ミューテーション認可。</param>
/// <param name="projectAuth">プロジェクト認可。</param>
/// <param name="definitions">定義リポジトリ（プロジェクト解決）。</param>
/// <param name="executor">トランザクション実行。</param>
/// <param name="resourceGrantAuth">Principal の実行リソース許可。</param>
/// <param name="tenantContext">呼び出しスコープのテナント文脈。Principal の無い Worker 経路を見分ける。</param>
internal sealed class ExecutionAuthorizationGuard(
    IRuntimePermissionAuthorization runtimeAuth,
    IExecutionMutationAuthorization mutationAuth,
    IProjectAuthorizationService projectAuth,
    IDefinitionRepository definitions,
    ICoreTransactionExecutor executor,
    PrincipalResourceGrantAuthorization resourceGrantAuth,
    ITenantContextAccessor tenantContext)
{
    /// <summary>Runtime の executions:read を要求する。</summary>
    /// <param name="ct">キャンセル。</param>
    public Task EnsureExecutionsReadAsync(CancellationToken ct) =>
        runtimeAuth.EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsRead, ct);

    /// <summary>Runtime の executions:write を要求する。</summary>
    /// <param name="ct">キャンセル。</param>
    public Task EnsureExecutionsWriteAsync(CancellationToken ct) =>
        runtimeAuth.EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsWrite, ct);

    /// <summary>実行行の security snapshot に基づきミューテーション権限を要求する。</summary>
    /// <param name="execution">実行投影行。</param>
    /// <param name="ct">キャンセル。</param>
    public Task EnsureExecutionMutationWriteAsync(ExecutionRow execution, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(execution);
        var snapshot = ExecutionSecuritySnapshotJson.TryDeserialize(execution.SecuritySnapshotJson);
        return mutationAuth.EnsureMutationPermissionAsync(
            snapshot,
            RuntimePermissionRequirements.ExecutionsWrite,
            ct);
    }

    /// <summary>定義が属するプロジェクト上で実行開始可能かを要求する。</summary>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="NotFoundException">定義が見つからない、または論理削除済みのとき。</exception>
    public Task EnsureCanExecuteOnDefinitionAsync(
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct) =>
        executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var projectId = await definitions
                    .ResolveProjectIdAsync(uow, tenantId, definitionId, innerCt)
                    .ConfigureAwait(false);
                if (projectId is null)
                    throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

                await projectAuth
                    .EnsureCanExecuteAsync(uow, tenantId, projectId.Value, innerCt)
                    .ConfigureAwait(false);
            },
            ct);

    /// <summary>定義が属するプロジェクト上で読取可能かを、既存 UoW 内で要求する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="NotFoundException">定義が無い、または Project 未所属のとき。</exception>
    /// <exception cref="ForbiddenException">許可集合に含まれないとき。コードは <c>RESOURCE_GRANT_DENIED</c>。</exception>
    public async Task EnsureCanReadOnDefinitionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        var projectId = await definitions
            .ResolveProjectIdIncludingDeletedAsync(uow, tenantId, definitionId, ct)
            .ConfigureAwait(false);
        if (projectId is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        await projectAuth
            .EnsureCanReadAsync(uow, tenantId, projectId.Value, ct)
            .ConfigureAwait(false);
        await resourceGrantAuth.EnsureAsync(projectId.Value, definitionId, ct).ConfigureAwait(false);
    }

    /// <summary>定義が属するプロジェクト上で読取可能かを、読み取りトランザクションで要求する。</summary>
    /// <remarks>削除済み定義も所属 Project で判定する。スケジュールの単票取得から呼ぶ。</remarks>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="NotFoundException">定義が無い、または Project 未所属のとき。</exception>
    /// <exception cref="ForbiddenException">許可集合に含まれないとき。コードは <c>RESOURCE_GRANT_DENIED</c>。</exception>
    public Task EnsureCanReadOnDefinitionAsync(Guid tenantId, Guid definitionId, CancellationToken ct) =>
        executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => EnsureCanReadOnDefinitionAsync(uow, tenantId, definitionId, innerCt),
            ct);

    /// <summary>削除済み定義も含めて所属 Project を解決し、呼び出し元に executor 以上を要求する。</summary>
    /// <remarks>
    /// 既存スケジュールへの書き込み（更新・削除・手動実行）から呼ぶ。
    /// 定義が論理削除されても、スケジュールを無効化・削除できるようにするため。
    /// </remarks>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="NotFoundException">定義行が無い、または Project 未所属のとき。</exception>
    /// <exception cref="ForbiddenException">所属が executor 未満のとき。コードは <c>PROJECT_ACCESS_DENIED</c>。</exception>
    public Task EnsureCanExecuteOnDefinitionIncludingDeletedAsync(
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct) =>
        executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var projectId = await definitions
                    .ResolveProjectIdIncludingDeletedAsync(uow, tenantId, definitionId, innerCt)
                    .ConfigureAwait(false);
                if (projectId is null)
                    throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

                await projectAuth
                    .EnsureCanExecuteAsync(uow, tenantId, projectId.Value, innerCt)
                    .ConfigureAwait(false);
            },
            ct);

    /// <summary>
    /// User / ServiceAccount の Start に、project 許可と definition 許可の AND を要求する。
    /// </summary>
    /// <remarks>
    /// 種別の行が無いとその種別は追加制限なし。System と未分類の Principal は評価しない。
    /// 継承子 Start からは呼ばない。
    /// </remarks>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="definitionId">論理定義 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="NotFoundException">定義が見つからないとき。</exception>
    /// <exception cref="ForbiddenException">許可集合に含まれないとき。コードは <c>RESOURCE_GRANT_DENIED</c>。</exception>
    public async Task EnsureResourceGrantAsync(Guid tenantId, Guid definitionId, CancellationToken ct)
    {
        var projectId = await RequireProjectIdAsync(tenantId, definitionId, ct).ConfigureAwait(false);
        await resourceGrantAuth.EnsureAsync(projectId, definitionId, ct).ConfigureAwait(false);
    }

    /// <summary>指定 Principal の Start に、project 許可と definition 許可の AND を要求する。</summary>
    /// <remarks>スケジュールの作成・更新で、呼び出し元ではなく run-as ServiceAccount を評価する。</remarks>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="principalId">評価する Principal。</param>
    /// <param name="definitionId">論理定義 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="NotFoundException">定義が見つからないとき。</exception>
    /// <exception cref="ForbiddenException">許可集合に含まれないとき。コードは <c>RESOURCE_GRANT_DENIED</c>。</exception>
    public async Task EnsureResourceGrantForPrincipalAsync(
        Guid tenantId,
        Guid principalId,
        Guid definitionId,
        CancellationToken ct)
    {
        var projectId = await RequireProjectIdAsync(tenantId, definitionId, ct).ConfigureAwait(false);
        await resourceGrantAuth
            .EnsureForPrincipalAsync(principalId, projectId, definitionId, ct)
            .ConfigureAwait(false);
    }

    /// <summary>既存 execution の Resume / Cancel に、生存中の許可集合を要求する。</summary>
    /// <remarks>削除済み定義でも所属 project を解決する。セキュリティ snapshot は見ない。</remarks>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="definitionId">論理定義 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="NotFoundException">定義行が無いとき。</exception>
    /// <exception cref="ForbiddenException">許可集合に含まれないとき。コードは <c>RESOURCE_GRANT_DENIED</c>。</exception>
    public async Task EnsureResourceGrantForExistingExecutionAsync(
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        var projectId = await executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => definitions.ResolveProjectIdIncludingDeletedAsync(
                    uow,
                    tenantId,
                    definitionId,
                    innerCt),
                ct)
            .ConfigureAwait(false);
        if (projectId is null)
            throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

        await resourceGrantAuth.EnsureAsync(projectId.Value, definitionId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 既存 execution のミューテーションに、定義が属する project で executor 以上を要求する。
    /// </summary>
    /// <remarks>
    /// <para>Cancel / Resume / イベント送信 / 復旧から、<see cref="EnsureExecutionMutationWriteAsync"/> のあとに呼ぶ。</para>
    /// <para>削除済み定義でも所属 project を解決する。Owner も Live の所属を見る（開始後に外れたら操作できない）。</para>
    /// <para>Principal の無い Worker 経路は、受理時に HTTP 側で判定済みのため見ない。</para>
    /// </remarks>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="definitionId">execution の論理定義 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="NotFoundException">定義行が無い、または Project 未所属のとき。</exception>
    /// <exception cref="ForbiddenException">所属が executor 未満のとき。コードは <c>PROJECT_ACCESS_DENIED</c>。</exception>
    public Task EnsureCanMutateOnExecutionProjectAsync(
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        if (tenantContext.PrincipalId is null)
            return Task.CompletedTask;

        return executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var projectId = await definitions
                    .ResolveProjectIdIncludingDeletedAsync(uow, tenantId, definitionId, innerCt)
                    .ConfigureAwait(false);
                if (projectId is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

                await projectAuth
                    .EnsureCanExecuteAsync(uow, tenantId, projectId.Value, innerCt)
                    .ConfigureAwait(false);
            },
            ct);
    }

    /// <summary>定義の所属 project を解決する。無ければ 404。</summary>
    private async Task<Guid> RequireProjectIdAsync(Guid tenantId, Guid definitionId, CancellationToken ct)
    {
        var projectId = await executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => definitions.ResolveProjectIdAsync(uow, tenantId, definitionId, innerCt),
                ct)
            .ConfigureAwait(false);
        if (projectId is null)
            throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);
        return projectId.Value;
    }
}
