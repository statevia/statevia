namespace Statevia.Core.Application.Services;

/// <inheritdoc />
internal sealed class ExecutionSecuritySnapshotFactory : IExecutionSecuritySnapshotFactory
{
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IPrincipalDataAccess _principalDataAccess;
    private readonly ICoreTransactionExecutor _executor;
    private readonly IDefinitionRepository _definitions;
    private readonly IProjectRepository _projects;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ExecutionSecuritySnapshotFactory(
        ITenantContextAccessor tenantContext,
        IPrincipalDataAccess principalDataAccess,
        ICoreTransactionExecutor executor,
        IDefinitionRepository definitions,
        IProjectRepository projects)
    {
        _tenantContext = tenantContext;
        _principalDataAccess = principalDataAccess;
        _executor = executor;
        _definitions = definitions;
        _projects = projects;
    }

    /// <inheritdoc />
    public async Task<ExecutionSecuritySnapshot> CaptureForStartAsync(
        Guid tenantId,
        Guid definitionId,
        DateTime capturedAt,
        CancellationToken cancellationToken)
    {
        if (_tenantContext.PrincipalId is not { } principalId)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        var principal = await _principalDataAccess
            .FindPrincipalAsync(principalId, cancellationToken)
            .ConfigureAwait(false);
        if (principal is null)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        PrincipalIdentityAuthorization.EnsureLive(principal);

        var effectiveKeys = await ResolveEffectivePermissionKeysAsync(principalId, cancellationToken)
            .ConfigureAwait(false);
        var permissionSetHash = PermissionSetHash.Compute(effectiveKeys);

        var isTenantAdmin = await _principalDataAccess
            .IsTenantAdminAsync(principalId, cancellationToken)
            .ConfigureAwait(false);
        var groupSnapshots = await _principalDataAccess
            .GetGroupSnapshotsForPrincipalAsync(principalId, cancellationToken)
            .ConfigureAwait(false);

        var authorizationContext = await ResolveAuthorizationContextAsync(
                tenantId,
                principalId,
                definitionId,
                groupSnapshots,
                isTenantAdmin,
                cancellationToken)
            .ConfigureAwait(false);

        return new ExecutionSecuritySnapshot
        {
            TenantId = tenantId,
            StartedByPrincipalId = principalId,
            PrincipalType = PrincipalTypeLabels.ToSnapshotLabel(principal.PrincipalType),
            EffectivePermissionKeys = effectiveKeys,
            PermissionSetHash = permissionSetHash,
            AuthorizationContext = authorizationContext,
            EvaluationMode = SecurityEvaluationMode.Snapshot,
            CapturedAt = capturedAt
        };
    }

    /// <inheritdoc />
    public async Task<ExecutionSecuritySnapshot> CaptureForChildWorkflowAsync(
        ExecutionSecuritySnapshot parentSnapshot,
        Guid tenantId,
        Guid childDefinitionId,
        Guid parentDefinitionId,
        DateTime capturedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parentSnapshot);
        if (parentSnapshot.TenantId != tenantId)
        {
            throw new InvalidOperationException("Parent security snapshot tenant does not match.");
        }

        var parentContext = parentSnapshot.AuthorizationContext;
        // Worker 文脈では Live Principal が無いため、子の Project ロールは親を開始した Principal の所属で引く。
        var authorizationContext = await ResolveAuthorizationContextAsync(
                tenantId,
                parentSnapshot.StartedByPrincipalId,
                childDefinitionId,
                parentContext.GroupSnapshots,
                parentContext.IsTenantAdmin,
                cancellationToken)
            .ConfigureAwait(false);

        return new ExecutionSecuritySnapshot
        {
            TenantId = tenantId,
            StartedByPrincipalId = parentSnapshot.StartedByPrincipalId,
            PrincipalType = parentSnapshot.PrincipalType,
            EffectivePermissionKeys = parentSnapshot.EffectivePermissionKeys,
            PermissionSetHash = parentSnapshot.PermissionSetHash,
            AuthorizationContext = authorizationContext,
            EvaluationMode = parentSnapshot.EvaluationMode,
            CapturedAt = capturedAt,
            CaptureReason = parentSnapshot.CaptureReason,
            AncestorDefinitionIds = MergeAncestorDefinitionIds(
                parentSnapshot.AncestorDefinitionIds,
                parentDefinitionId)
        };
    }

    /// <summary>定義の Project と、その Project での Principal の実効ロールを固定する。</summary>
    /// <param name="tenantId">テナント。</param>
    /// <param name="principalId">
    /// ロールを引く Principal。Start では要求元、子ワークフローでは親の開始者を渡す。
    /// Worker 文脈でも解決できるよう、テナント文脈の Live Principal には依存しない。
    /// </param>
    /// <param name="definitionId">開始する定義。</param>
    /// <param name="groupSnapshots">固定するグループ。</param>
    /// <param name="isTenantAdmin">テナント管理者か。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>認可文脈のスナップショット。</returns>
    /// <exception cref="NotFoundException">定義が無い、または Principal が定義の Project に所属していないとき。</exception>
    private async Task<AuthorizationContextSnapshot> ResolveAuthorizationContextAsync(
        Guid tenantId,
        Guid principalId,
        Guid definitionId,
        IReadOnlyList<GroupSnapshot> groupSnapshots,
        bool isTenantAdmin,
        CancellationToken cancellationToken) =>
        await _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var projectId = await _definitions
                    .ResolveProjectIdAsync(uow, tenantId, definitionId, innerCt)
                    .ConfigureAwait(false);
                if (projectId is null)
                    throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

                var projectRole = await _projects
                    .ResolveEffectiveRoleAsync(uow, tenantId, principalId, projectId.Value, innerCt)
                    .ConfigureAwait(false);
                if (projectRole is null)
                    throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

                return new AuthorizationContextSnapshot
                {
                    ProjectId = projectId.Value,
                    ProjectRole = ProjectAccessRolePolicy.ToStorageValue(projectRole.Value),
                    GroupSnapshots = groupSnapshots,
                    IsTenantAdmin = isTenantAdmin
                };
            },
            cancellationToken).ConfigureAwait(false);

    private static Guid[] MergeAncestorDefinitionIds(
        IReadOnlyList<Guid>? existing,
        Guid parentDefinitionId)
    {
        var source = existing ?? [];
        return source
            .Append(parentDefinitionId)
            .Distinct()
            .ToArray();
    }

    private async Task<IReadOnlyList<string>> ResolveEffectivePermissionKeysAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (_tenantContext.EffectivePermissionKeys is { } fixedKeys)
            return fixedKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray();

        var expanded = await _principalDataAccess
            .ExpandPrincipalPermissionKeysAsync(principalId, cancellationToken)
            .ConfigureAwait(false);
        return expanded.OrderBy(key => key, StringComparer.Ordinal).ToArray();
    }
}
