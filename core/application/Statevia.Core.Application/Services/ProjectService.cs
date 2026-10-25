using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Statevia.Core.Application.Services;

/// <summary>テナント内 Project の所属一覧・取得・作成・部分更新。</summary>
/// <remarks>
/// <para>一覧・取得は呼び出し元 Principal のメンバー行に限定する。未所属は 404。</para>
/// <para>作成は <c>projects.create</c>。作成者を <c>admin</c> で同一トランザクションに挿入する。</para>
/// <para>PATCH は <c>project.admin</c> のみ（tenant.admin は注入しない）。DELETE は提供しない。</para>
/// </remarks>
/// <param name="projects">projects / project_accesses 永続化。</param>
/// <param name="projectAuth">Project ロール認可。</param>
/// <param name="tenantContext">現在テナント・Principal。</param>
/// <param name="runtimeAuth">Runtime permission（作成時の <c>projects.create</c>）。</param>
/// <param name="idGenerator">Project UUID 生成。</param>
/// <param name="executor">コア DB トランザクション。</param>
/// <param name="tenantAdmin">テナント管理者判定（メンバー break-glass と全件一覧）。</param>
/// <param name="logger">メンバー変更の監査。</param>
[SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "DI による明示的コンストラクタ注入。")]
internal sealed class ProjectService(
    IProjectRepository projects,
    IProjectAuthorizationService projectAuth,
    ITenantContextAccessor tenantContext,
    IRuntimePermissionAuthorization runtimeAuth,
    IIdGenerator idGenerator,
    ICoreTransactionExecutor executor,
    ITenantAdminAuthorization tenantAdmin,
    ILogger<ProjectService> logger) : IProjectService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ProjectResponse>> ListAsync(CancellationToken ct)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var principalId = tenantContext.GetRequiredPrincipalId();

        return executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var rows = await projects
                    .ListMembershipAsync(uow, tenantId, principalId, innerCt)
                    .ConfigureAwait(false);
                return (IReadOnlyList<ProjectResponse>)rows
                    .Select(row => ToResponse(row.Project, row.Role))
                    .ToList();
            },
            ct);
    }

    /// <inheritdoc />
    public Task<ProjectResponse> GetAsync(Guid projectId, CancellationToken ct)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var principalId = tenantContext.GetRequiredPrincipalId();

        return executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                await projectAuth
                    .EnsureCanReadAsync(uow, tenantId, projectId, innerCt)
                    .ConfigureAwait(false);

                var project = await projects
                    .GetInTenantAsync(uow, tenantId, projectId, innerCt)
                    .ConfigureAwait(false);
                if (project is null)
                    throw new NotFoundException(ProjectAuthorizationMessages.ProjectNotFound);

                var role = await RequireEffectiveRoleAsync(uow, tenantId, principalId, projectId, innerCt)
                    .ConfigureAwait(false);
                return ToResponse(project, role);
            },
            ct);
    }

    /// <inheritdoc />
    public async Task<ProjectResponse> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await runtimeAuth
            .EnsurePermissionAsync(RuntimePermissionRequirements.ProjectsCreate, ct)
            .ConfigureAwait(false);

        var tenantId = tenantContext.GetRequiredTenantId();
        var principalId = tenantContext.GetRequiredPrincipalId();
        var slug = request.Slug.Trim();
        var displayName = request.DisplayName.Trim();
        var description = NormalizeOptionalText(request.Description ?? string.Empty);
        var projectId = idGenerator.NewSequentialGuid();
        var now = DateTime.UtcNow;

        return await executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                if (await projects
                        .ExistsSlugInTenantAsync(uow, tenantId, slug, excludingProjectId: null, innerCt)
                        .ConfigureAwait(false))
                {
                    throw new ApiValidationException(
                        "slug must be unique in the tenant.",
                        new { field = "slug" });
                }

                var project = new ProjectRow
                {
                    ProjectId = projectId,
                    TenantId = tenantId,
                    Slug = slug,
                    DisplayName = displayName,
                    Description = description,
                    CreatedAt = now
                };
                await projects.AddAsync(uow, project, innerCt).ConfigureAwait(false);
                await projects
                    .GrantAccessAsync(uow, projectId, principalId, ProjectAccessRole.Admin, innerCt)
                    .ConfigureAwait(false);

                return ToResponse(project, ProjectAccessRole.Admin);
            },
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<ProjectResponse> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.GetRequiredTenantId();
        var principalId = tenantContext.GetRequiredPrincipalId();

        return executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                await projectAuth
                    .EnsureCanAdminAsync(uow, tenantId, projectId, innerCt)
                    .ConfigureAwait(false);

                var project = await RequireProjectForUpdateAsync(uow, tenantId, projectId, innerCt)
                    .ConfigureAwait(false);
                await ApplyPartialUpdateAsync(uow, tenantId, project, request, innerCt)
                    .ConfigureAwait(false);
                var role = await RequireEffectiveRoleAsync(uow, tenantId, principalId, projectId, innerCt)
                    .ConfigureAwait(false);
                return ToResponse(project, role);
            },
            ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProjectMemberResponse>> ListMembersAsync(Guid projectId, CancellationToken ct)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                await projectAuth
                    .EnsureCanManageMembersAsync(uow, tenantId, projectId, innerCt)
                    .ConfigureAwait(false);
                var members = await projects
                    .ListMemberDetailsAsync(uow, tenantId, projectId, innerCt)
                    .ConfigureAwait(false);
                return (IReadOnlyList<ProjectMemberResponse>)members
                    .Select(member => new ProjectMemberResponse
                    {
                        PrincipalId = member.PrincipalId,
                        DisplayName = member.DisplayName,
                        Kind = ToMemberKind(member.PrincipalType),
                        Role = ProjectAccessRolePolicy.ToStorageValue(member.Role),
                        CreatedAt = member.CreatedAt
                    })
                    .ToList();
            },
            ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProjectGrantCandidateResponse>> ListGrantCandidatesAsync(
        Guid projectId,
        CancellationToken ct)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                await projectAuth
                    .EnsureCanManageMembersAsync(uow, tenantId, projectId, innerCt)
                    .ConfigureAwait(false);
                var candidates = await projects
                    .ListGrantCandidatesAsync(uow, tenantId, innerCt)
                    .ConfigureAwait(false);
                return (IReadOnlyList<ProjectGrantCandidateResponse>)candidates
                    .Select(candidate => new ProjectGrantCandidateResponse
                    {
                        PrincipalId = candidate.PrincipalId,
                        DisplayName = candidate.DisplayName,
                        Kind = ToMemberKind(candidate.PrincipalType)
                    })
                    .ToList();
            },
            ct);
    }

    /// <inheritdoc />
    public Task UpsertMemberAsync(
        Guid projectId,
        Guid principalId,
        UpsertProjectMemberRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var role = ParseStorageRole(request.Role);
        var tenantId = tenantContext.GetRequiredTenantId();
        var actorPrincipalId = tenantContext.GetRequiredPrincipalId();

        return executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                await projectAuth
                    .EnsureCanManageMembersAsync(uow, tenantId, projectId, innerCt)
                    .ConfigureAwait(false);
                await EnsureCanChangeLastAdminAsync(
                        uow, actorPrincipalId, projectId, principalId, role, innerCt)
                    .ConfigureAwait(false);
                await projects
                    .UpsertAccessAsync(uow, projectId, principalId, role, innerCt)
                    .ConfigureAwait(false);
                logger.ProjectMemberUpserted(
                    actorPrincipalId,
                    projectId,
                    principalId,
                    ProjectAccessRolePolicy.ToStorageValue(role));
            },
            ct);
    }

    /// <inheritdoc />
    public Task RemoveMemberAsync(Guid projectId, Guid principalId, CancellationToken ct)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var actorPrincipalId = tenantContext.GetRequiredPrincipalId();

        return executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                await projectAuth
                    .EnsureCanManageMembersAsync(uow, tenantId, projectId, innerCt)
                    .ConfigureAwait(false);
                await EnsureCanChangeLastAdminAsync(
                        uow, actorPrincipalId, projectId, principalId, nextRole: null, innerCt)
                    .ConfigureAwait(false);
                var removed = await projects
                    .RemoveAccessAsync(uow, projectId, principalId, innerCt)
                    .ConfigureAwait(false);
                if (!removed)
                    throw new NotFoundException(ProjectAuthorizationMessages.ProjectNotFound);

                logger.ProjectMemberRemoved(actorPrincipalId, projectId, principalId);
            },
            ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectResponse>> ListAllForTenantAdminAsync(CancellationToken ct)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var principalId = tenantContext.GetRequiredPrincipalId();
        if (!await tenantAdmin.IsTenantAdminAsync(principalId, ct).ConfigureAwait(false))
            throw new ForbiddenException("Tenant administrator required.", "FORBIDDEN");

        return await executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var projectsInTenant = await projects
                    .ListInTenantAsync(uow, tenantId, innerCt)
                    .ConfigureAwait(false);
                var membership = await projects
                    .ListMembershipAsync(uow, tenantId, principalId, innerCt)
                    .ConfigureAwait(false);
                var roles = membership.ToDictionary(row => row.Project.ProjectId, row => row.Role);
                return (IReadOnlyList<ProjectResponse>)projectsInTenant
                    .Select(project => ToResponse(
                        project,
                        roles.TryGetValue(project.ProjectId, out var role) ? role : null))
                    .ToList();
            },
            ct).ConfigureAwait(false);
    }

    private async Task<ProjectRow> RequireProjectForUpdateAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct)
    {
        var project = await projects
            .GetInTenantForUpdateAsync(uow, tenantId, projectId, ct)
            .ConfigureAwait(false);
        return project
            ?? throw new NotFoundException(ProjectAuthorizationMessages.ProjectNotFound);
    }

    private async Task<ProjectAccessRole> RequireEffectiveRoleAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        Guid projectId,
        CancellationToken ct)
    {
        var role = await projects
            .ResolveEffectiveRoleAsync(uow, tenantId, principalId, projectId, ct)
            .ConfigureAwait(false);
        return role
            ?? throw new NotFoundException(ProjectAuthorizationMessages.ProjectNotFound);
    }

    private async Task ApplyPartialUpdateAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        ProjectRow project,
        UpdateProjectRequest request,
        CancellationToken ct)
    {
        if (request.DisplayName is { } displayNameRaw)
            project.DisplayName = displayNameRaw.Trim();

        if (request.Description is { } descriptionRaw)
            project.Description = NormalizeOptionalText(descriptionRaw);

        if (request.Slug is { } slugRaw)
            await ApplySlugUpdateAsync(uow, tenantId, project, slugRaw, ct).ConfigureAwait(false);
    }

    private async Task ApplySlugUpdateAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        ProjectRow project,
        string slugRaw,
        CancellationToken ct)
    {
        var slug = slugRaw.Trim();
        if (await projects
                .ExistsSlugInTenantAsync(uow, tenantId, slug, project.ProjectId, ct)
                .ConfigureAwait(false))
        {
            throw new ApiValidationException(
                "slug must be unique in the tenant.",
                new { field = "slug" });
        }

        project.Slug = slug;
    }

    private static string? NormalizeOptionalText(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// 最後の admin を外す、または admin 以外へ下げる操作は tenant.admin だけが通す。
    /// </summary>
    private async Task EnsureCanChangeLastAdminAsync(
        ICoreUnitOfWork uow,
        Guid actorPrincipalId,
        Guid projectId,
        Guid targetPrincipalId,
        ProjectAccessRole? nextRole,
        CancellationToken ct)
    {
        if (nextRole is ProjectAccessRole.Admin)
            return;

        var current = await projects
            .GetAccessForUpdateAsync(uow, projectId, targetPrincipalId, ct)
            .ConfigureAwait(false);
        if (current is not { Role: ProjectAccessRole.Admin })
            return;

        var adminCount = await projects
            .CountByRoleAsync(uow, projectId, ProjectAccessRole.Admin, ct)
            .ConfigureAwait(false);
        if (adminCount > 1)
            return;

        if (await tenantAdmin.IsTenantAdminAsync(actorPrincipalId, ct).ConfigureAwait(false))
            return;

        throw new ApiValidationException(
            "The last project admin can only be removed by a tenant administrator.",
            new { field = "principalId" });
    }

    /// <summary>画面の種別。ユーザーと API キー以外は system。</summary>
    private static string ToMemberKind(PrincipalType principalType) => principalType switch
    {
        PrincipalType.User => "user",
        PrincipalType.ServiceAccount => "apiKey",
        PrincipalType.System => "system",
        _ => throw new ArgumentOutOfRangeException(nameof(principalType), principalType, null)
    };

    private static ProjectAccessRole ParseStorageRole(string role)
    {
        var normalized = role.Trim();
        var matched = Enum.GetValues<ProjectAccessRole>()
            .Select(candidate => new
            {
                candidate,
                storage = ProjectAccessRolePolicy.ToStorageValue(candidate)
            })
            .FirstOrDefault(pair => pair.storage.Equals(normalized, StringComparison.Ordinal));
        if (matched is null)
        {
            throw new ApiValidationException(
                "role must be reader, executor, publisher, or admin.",
                new { field = "role" });
        }

        return matched.candidate;
    }

    private static ProjectResponse ToResponse(ProjectRow project, ProjectAccessRole? role) =>
        new()
        {
            ProjectId = project.ProjectId,
            Slug = project.Slug,
            DisplayName = project.DisplayName,
            Description = project.Description,
            CreatedAt = project.CreatedAt,
            Role = role is { } value ? ProjectAccessRolePolicy.ToStorageValue(value) : null
        };
}
