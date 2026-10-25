namespace Statevia.Core.Application.Services;

/// <summary><see cref="IProjectAuthorizationService"/> — Principal のメンバー行だけを truth とする。</summary>
internal sealed class ProjectAuthorizationService : IProjectAuthorizationService
{
    private readonly IProjectRepository _projects;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ITenantAdminAuthorization _tenantAdmin;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="projects">Project 永続化。</param>
    /// <param name="tenantContext">呼び出し元の Principal。</param>
    /// <param name="tenantAdmin">テナント管理者判定。</param>
    public ProjectAuthorizationService(
        IProjectRepository projects,
        ITenantContextAccessor tenantContext,
        ITenantAdminAuthorization tenantAdmin)
    {
        _projects = projects;
        _tenantContext = tenantContext;
        _tenantAdmin = tenantAdmin;
    }

    /// <inheritdoc />
    public async Task EnsureCanReadAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        await EnsureMinimumRoleAsync(
            uow,
            tenantId,
            projectId,
            ProjectAccessRole.Reader,
            forbiddenWhenBelowMinimum: false,
            ct).ConfigureAwait(false);

    /// <inheritdoc />
    public Task EnsureCanExecuteAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        EnsureMinimumRoleAsync(
            uow,
            tenantId,
            projectId,
            ProjectAccessRole.Executor,
            forbiddenWhenBelowMinimum: true,
            ct);

    /// <inheritdoc />
    public Task EnsureCanPublishAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        EnsureMinimumRoleAsync(
            uow,
            tenantId,
            projectId,
            ProjectAccessRole.Publisher,
            forbiddenWhenBelowMinimum: true,
            ct);

    /// <inheritdoc />
    public async Task EnsureCanManageMembersAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct)
    {
        if (_tenantContext.PrincipalId is not { } principalId)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        if (!await _projects.ExistsInTenantAsync(uow, tenantId, projectId, ct).ConfigureAwait(false))
            throw new NotFoundException(ProjectAuthorizationMessages.ProjectNotFound);

        if (await _tenantAdmin.IsTenantAdminAsync(principalId, ct).ConfigureAwait(false))
            return;

        var effectiveRole = await _projects
            .ResolveEffectiveRoleAsync(uow, tenantId, principalId, projectId, ct)
            .ConfigureAwait(false);

        if (effectiveRole is ProjectAccessRole.Admin)
            return;

        if (effectiveRole is not null && effectiveRole.Value >= ProjectAccessRole.Reader)
            throw new ForbiddenException(ProjectAuthorizationMessages.InsufficientProjectRole, "PROJECT_ACCESS_DENIED");

        throw new NotFoundException(ProjectAuthorizationMessages.ProjectNotFound);
    }

    /// <inheritdoc />
    public Task EnsureCanAdminAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        EnsureMinimumRoleAsync(
            uow,
            tenantId,
            projectId,
            ProjectAccessRole.Admin,
            forbiddenWhenBelowMinimum: true,
            ct);

    private async Task EnsureMinimumRoleAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        ProjectAccessRole minimumRole,
        bool forbiddenWhenBelowMinimum,
        CancellationToken ct)
    {
        if (_tenantContext.PrincipalId is not { } principalId)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        var effectiveRole = await _projects
            .ResolveEffectiveRoleAsync(uow, tenantId, principalId, projectId, ct)
            .ConfigureAwait(false);

        if (effectiveRole is null)
            throw new NotFoundException(ProjectAuthorizationMessages.ProjectNotFound);

        if (ProjectAccessRolePolicy.MeetsMinimum(effectiveRole.Value, minimumRole))
            return;

        if (forbiddenWhenBelowMinimum && effectiveRole.Value >= ProjectAccessRole.Reader)
            throw new ForbiddenException(ProjectAuthorizationMessages.InsufficientProjectRole, "PROJECT_ACCESS_DENIED");

        throw new NotFoundException(ProjectAuthorizationMessages.ProjectNotFound);
    }
}

/// <summary>project 認可メッセージ。</summary>
internal static class ProjectAuthorizationMessages
{
    /// <summary>越境・未付与時（存在秘匿）。</summary>
    public const string ProjectNotFound = "Project not found";

    /// <summary>ロール不足（Reader が Executor 操作を試行等）。</summary>
    public const string InsufficientProjectRole = "Insufficient project access role.";
}
