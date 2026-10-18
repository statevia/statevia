using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Contracts.Admin;
using Statevia.Service.Api.Services;

namespace Statevia.Service.Api.Controllers;

/// <summary>テナント管理者向け users / groups / ServiceAccount / リソース許可 API。</summary>
[ApiController]
[Route("v1/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly ITenantAdministrationService _administration;
    private readonly ITenantNotificationSmtpService _notificationSmtp;
    private readonly ITenantActionBindingService _actionBindings;
    private readonly ITenantContextAccessor _tenantContext;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="administration">ユーザー、グループ、API キー、ServiceAccount。</param>
    /// <param name="notificationSmtp">通知 SMTP。</param>
    /// <param name="actionBindings">Action の変数とシークレット。</param>
    /// <param name="tenantContext">呼び出し主体。</param>
    public AdminController(
        ITenantAdministrationService administration,
        ITenantNotificationSmtpService notificationSmtp,
        ITenantActionBindingService actionBindings,
        ITenantContextAccessor tenantContext)
    {
        _administration = administration;
        _notificationSmtp = notificationSmtp;
        _actionBindings = actionBindings;
        _tenantContext = tenantContext;
    }

    /// <summary>GET /v1/admin/permissions — 権限カタログ。</summary>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PermissionDefinitionDto>>> ListPermissions(CancellationToken ct) =>
        Ok(await _administration.ListPermissionsAsync(RequirePrincipalId(), ct).ConfigureAwait(false));

    /// <summary>GET /v1/admin/users — ユーザー一覧。</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminUserListItemDto>>> ListUsers(CancellationToken ct) =>
        Ok(await _administration.ListUsersAsync(RequirePrincipalId(), ct).ConfigureAwait(false));

    /// <summary>POST /v1/admin/users — ユーザー作成。</summary>
    [HttpPost("users")]
    [ProducesResponseType(typeof(AdminUserListItemDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminUserListItemDto>> CreateUser(
        [FromBody] CreateAdminUserRequest request,
        CancellationToken ct)
    {
        var created = await _administration.CreateUserAsync(RequirePrincipalId(), request, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(ListUsers), created);
    }

    /// <summary>PATCH /v1/admin/users/{userId} — ユーザー更新。</summary>
    [HttpPatch("users/{userId:guid}")]
    [ProducesResponseType(typeof(AdminUserListItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminUserListItemDto>> UpdateUser(
        Guid userId,
        [FromBody] UpdateAdminUserRequest request,
        CancellationToken ct) =>
        Ok(await _administration.UpdateUserAsync(RequirePrincipalId(), userId, request, ct).ConfigureAwait(false));

    /// <summary>PUT /v1/admin/users/{userId}/password — パスワード上書き。</summary>
    [HttpPut("users/{userId:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateUserPassword(
        Guid userId,
        [FromBody] UpdateAdminUserPasswordRequest request,
        CancellationToken ct)
    {
        await _administration.UpdateUserPasswordAsync(RequirePrincipalId(), userId, request, ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>GET /v1/admin/groups — グループ一覧。</summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminGroupListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminGroupListItemDto>>> ListGroups(CancellationToken ct) =>
        Ok(await _administration.ListGroupsAsync(RequirePrincipalId(), ct).ConfigureAwait(false));

    /// <summary>POST /v1/admin/groups — グループ作成。</summary>
    [HttpPost("groups")]
    [ProducesResponseType(typeof(AdminGroupDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminGroupDetailDto>> CreateGroup(
        [FromBody] CreateAdminGroupRequest request,
        CancellationToken ct)
    {
        var created = await _administration.CreateGroupAsync(RequirePrincipalId(), request, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetGroup), new { groupId = created.GroupId }, created);
    }

    /// <summary>GET /v1/admin/groups/{groupId} — グループ詳細。</summary>
    [HttpGet("groups/{groupId:guid}")]
    [ProducesResponseType(typeof(AdminGroupDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminGroupDetailDto>> GetGroup(Guid groupId, CancellationToken ct) =>
        Ok(await _administration.GetGroupAsync(RequirePrincipalId(), groupId, ct).ConfigureAwait(false));

    /// <summary>PUT /v1/admin/groups/{groupId}/members — メンバー置換。</summary>
    [HttpPut("groups/{groupId:guid}/members")]
    [ProducesResponseType(typeof(AdminGroupDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminGroupDetailDto>> SetGroupMembers(
        Guid groupId,
        [FromBody] SetAdminGroupMembersRequest request,
        CancellationToken ct) =>
        Ok(await _administration.SetGroupMembersAsync(RequirePrincipalId(), groupId, request, ct).ConfigureAwait(false));

    /// <summary>PUT /v1/admin/groups/{groupId}/permissions — 権限置換。</summary>
    [HttpPut("groups/{groupId:guid}/permissions")]
    [ProducesResponseType(typeof(AdminGroupDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminGroupDetailDto>> SetGroupPermissions(
        Guid groupId,
        [FromBody] SetAdminGroupPermissionsRequest request,
        CancellationToken ct) =>
        Ok(await _administration.SetGroupPermissionsAsync(RequirePrincipalId(), groupId, request, ct).ConfigureAwait(false));

    /// <summary>GET /v1/admin/api-keys — API キー一覧（平文なし）。</summary>
    [HttpGet("api-keys")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminApiKeyListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminApiKeyListItemDto>>> ListApiKeys(CancellationToken ct) =>
        Ok(await _administration.ListApiKeysAsync(RequirePrincipalId(), ct).ConfigureAwait(false));

    /// <summary>POST /v1/admin/api-keys — API キー発行（平文は応答のみ）。</summary>
    [HttpPost("api-keys")]
    [ProducesResponseType(typeof(CreatedAdminApiKeyDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedAdminApiKeyDto>> CreateApiKey(
        [FromBody] CreateAdminApiKeyRequest request,
        CancellationToken ct)
    {
        var created = await _administration.CreateApiKeyAsync(RequirePrincipalId(), request, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(ListApiKeys), created);
    }

    /// <summary>DELETE /v1/admin/api-keys/{apiKeyId} — API キー失効。</summary>
    [HttpDelete("api-keys/{apiKeyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeApiKey(Guid apiKeyId, CancellationToken ct)
    {
        await _administration.RevokeApiKeyAsync(RequirePrincipalId(), apiKeyId, ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>GET /v1/admin/service-accounts — ServiceAccount 一覧（平文なし）。</summary>
    [HttpGet("service-accounts")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminServiceAccountListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminServiceAccountListItemDto>>> ListServiceAccounts(
        CancellationToken ct) =>
        Ok(await _administration.ListServiceAccountsAsync(RequirePrincipalId(), ct).ConfigureAwait(false));

    /// <summary>POST /v1/admin/service-accounts — 資格のない ServiceAccount 作成。</summary>
    [HttpPost("service-accounts")]
    [ProducesResponseType(typeof(AdminServiceAccountListItemDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminServiceAccountListItemDto>> CreateServiceAccount(
        [FromBody] CreateAdminServiceAccountRequest request,
        CancellationToken ct)
    {
        var created = await _administration.CreateServiceAccountAsync(RequirePrincipalId(), request, ct)
            .ConfigureAwait(false);
        return CreatedAtAction(nameof(GetServiceAccount), new { serviceAccountId = created.ServiceAccountId }, created);
    }

    /// <summary>GET /v1/admin/service-accounts/{serviceAccountId} — ServiceAccount 詳細。</summary>
    [HttpGet("service-accounts/{serviceAccountId:guid}")]
    [ProducesResponseType(typeof(AdminServiceAccountListItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminServiceAccountListItemDto>> GetServiceAccount(
        Guid serviceAccountId,
        CancellationToken ct) =>
        Ok(await _administration.GetServiceAccountAsync(RequirePrincipalId(), serviceAccountId, ct)
            .ConfigureAwait(false));

    /// <summary>PATCH /v1/admin/service-accounts/{serviceAccountId} — 有効・表示名・所属の更新。</summary>
    [HttpPatch("service-accounts/{serviceAccountId:guid}")]
    [ProducesResponseType(typeof(AdminServiceAccountListItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminServiceAccountListItemDto>> UpdateServiceAccount(
        Guid serviceAccountId,
        [FromBody] UpdateAdminServiceAccountRequest request,
        CancellationToken ct) =>
        Ok(await _administration.UpdateServiceAccountAsync(RequirePrincipalId(), serviceAccountId, request, ct)
            .ConfigureAwait(false));

    /// <summary>GET /v1/admin/users/{userId}/resource-grants — ユーザーの実行リソース許可。</summary>
    [HttpGet("users/{userId:guid}/resource-grants")]
    [ProducesResponseType(typeof(PrincipalResourceGrantsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrincipalResourceGrantsDto>> GetUserResourceGrants(
        Guid userId,
        CancellationToken ct) =>
        Ok(await _administration.GetUserResourceGrantsAsync(RequirePrincipalId(), userId, ct).ConfigureAwait(false));

    /// <summary>PUT /v1/admin/users/{userId}/resource-grants — ユーザーの実行リソース許可を一括置換する。</summary>
    [HttpPut("users/{userId:guid}/resource-grants")]
    [ProducesResponseType(typeof(PrincipalResourceGrantsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrincipalResourceGrantsDto>> ReplaceUserResourceGrants(
        Guid userId,
        [FromBody] ReplacePrincipalResourceGrantsRequest request,
        CancellationToken ct) =>
        Ok(await _administration.ReplaceUserResourceGrantsAsync(RequirePrincipalId(), userId, request, ct)
            .ConfigureAwait(false));

    /// <summary>GET /v1/admin/service-accounts/{serviceAccountId}/resource-grants — ServiceAccount の実行リソース許可。</summary>
    [HttpGet("service-accounts/{serviceAccountId:guid}/resource-grants")]
    [ProducesResponseType(typeof(PrincipalResourceGrantsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrincipalResourceGrantsDto>> GetServiceAccountResourceGrants(
        Guid serviceAccountId,
        CancellationToken ct) =>
        Ok(await _administration.GetServiceAccountResourceGrantsAsync(RequirePrincipalId(), serviceAccountId, ct)
            .ConfigureAwait(false));

    /// <summary>PUT /v1/admin/service-accounts/{serviceAccountId}/resource-grants — ServiceAccount の実行リソース許可を一括置換する。</summary>
    [HttpPut("service-accounts/{serviceAccountId:guid}/resource-grants")]
    [ProducesResponseType(typeof(PrincipalResourceGrantsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrincipalResourceGrantsDto>> ReplaceServiceAccountResourceGrants(
        Guid serviceAccountId,
        [FromBody] ReplacePrincipalResourceGrantsRequest request,
        CancellationToken ct) =>
        Ok(await _administration.ReplaceServiceAccountResourceGrantsAsync(
                RequirePrincipalId(),
                serviceAccountId,
                request,
                ct)
            .ConfigureAwait(false));

    /// <summary>GET /v1/admin/notification-smtp — 自テナントの通知 SMTP。パスワードは返さない。</summary>
    /// <param name="ct">キャンセル。</param>
    /// <returns>未設定でも 200。</returns>
    [HttpGet("notification-smtp")]
    [ProducesResponseType(typeof(TenantNotificationSmtpDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantNotificationSmtpDto>> GetNotificationSmtp(CancellationToken ct) =>
        Ok(await _notificationSmtp.GetAsync(RequirePrincipalId(), ct).ConfigureAwait(false));

    /// <summary>PUT /v1/admin/notification-smtp — 自テナントの通知 SMTP を全体置換する。</summary>
    /// <param name="request">ホスト、ポート、資格情報、既定 From。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>保存後の参照。パスワードは含まない。</returns>
    [HttpPut("notification-smtp")]
    [ProducesResponseType(typeof(TenantNotificationSmtpDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantNotificationSmtpDto>> ReplaceNotificationSmtp(
        [FromBody] PutTenantNotificationSmtpRequest request,
        CancellationToken ct) =>
        Ok(await _notificationSmtp.ReplaceAsync(RequirePrincipalId(), request, ct).ConfigureAwait(false));

    /// <summary>DELETE /v1/admin/notification-smtp — 自テナントの通知 SMTP を消す。</summary>
    /// <param name="ct">キャンセル。</param>
    /// <returns>行が無くても 204。</returns>
    [HttpDelete("notification-smtp")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteNotificationSmtp(CancellationToken ct)
    {
        await _notificationSmtp.DeleteAsync(RequirePrincipalId(), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>GET /v1/admin/action-bindings — 自テナントの変数とシークレット。シークレットの値は返さない。</summary>
    /// <param name="ct">キャンセル。</param>
    /// <returns>名前順の一覧。</returns>
    [HttpGet("action-bindings")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantActionBindingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantActionBindingDto>>> ListActionBindings(CancellationToken ct) =>
        Ok(await _actionBindings.ListAsync(RequirePrincipalId(), ct).ConfigureAwait(false));

    /// <summary>POST /v1/admin/action-bindings — 変数またはシークレットを作成する。</summary>
    /// <param name="request">名前、種別、値、許可 actionId。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>作成した行。シークレットの値は含まない。</returns>
    [HttpPost("action-bindings")]
    [ProducesResponseType(typeof(TenantActionBindingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantActionBindingDto>> CreateActionBinding(
        [FromBody] CreateTenantActionBindingRequest request,
        CancellationToken ct) =>
        Ok(await _actionBindings.CreateAsync(RequirePrincipalId(), request, ct).ConfigureAwait(false));

    /// <summary>PUT /v1/admin/action-bindings/{name} — 許可一覧を置換する。</summary>
    /// <param name="name">作成時の名前。変更しない。</param>
    /// <param name="request">種別、値、許可一覧。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>更新後の行。</returns>
    [HttpPut("action-bindings/{name}")]
    [ProducesResponseType(typeof(TenantActionBindingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantActionBindingDto>> UpdateActionBinding(
        string name,
        [FromBody] UpdateTenantActionBindingRequest request,
        CancellationToken ct) =>
        Ok(await _actionBindings.UpdateAsync(RequirePrincipalId(), name, request, ct).ConfigureAwait(false));

    /// <summary>DELETE /v1/admin/action-bindings/{name} — 行を消す。</summary>
    /// <param name="name">名前。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>行が無くても 204。</returns>
    [HttpDelete("action-bindings/{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteActionBinding(string name, CancellationToken ct)
    {
        await _actionBindings.DeleteAsync(RequirePrincipalId(), name, ct).ConfigureAwait(false);
        return NoContent();
    }

    private Guid RequirePrincipalId()
    {
        if (_tenantContext.IsResolved && _tenantContext.PrincipalId is { } principalId)
            return principalId;

        throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");
    }
}
