using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Contracts.Admin;
using System.Text.Json;

namespace Statevia.Service.Api.Services;

/// <summary>テナント管理者向け users / groups / ServiceAccount 管理。</summary>
public interface ITenantAdministrationService
{
    /// <summary>権限カタログを返す。</summary>
    Task<IReadOnlyList<PermissionDefinitionDto>> ListPermissionsAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken);

    /// <summary>テナント内ユーザーを一覧する。</summary>
    Task<IReadOnlyList<AdminUserListItemDto>> ListUsersAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken);

    /// <summary>ユーザーを作成する。</summary>
    Task<AdminUserListItemDto> CreateUserAsync(
        Guid callerPrincipalId,
        CreateAdminUserRequest request,
        CancellationToken cancellationToken);

    /// <summary>ユーザーを更新する（有効化・管理者フラグ）。</summary>
    Task<AdminUserListItemDto> UpdateUserAsync(
        Guid callerPrincipalId,
        Guid userId,
        UpdateAdminUserRequest request,
        CancellationToken cancellationToken);

    /// <summary>ユーザーのパスワードを上書きする。</summary>
    Task UpdateUserPasswordAsync(
        Guid callerPrincipalId,
        Guid userId,
        UpdateAdminUserPasswordRequest request,
        CancellationToken cancellationToken);

    /// <summary>グループを一覧する。</summary>
    Task<IReadOnlyList<AdminGroupListItemDto>> ListGroupsAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken);

    /// <summary>グループを作成する。</summary>
    Task<AdminGroupDetailDto> CreateGroupAsync(
        Guid callerPrincipalId,
        CreateAdminGroupRequest request,
        CancellationToken cancellationToken);

    /// <summary>グループ詳細を返す。</summary>
    Task<AdminGroupDetailDto> GetGroupAsync(
        Guid callerPrincipalId,
        Guid groupId,
        CancellationToken cancellationToken);

    /// <summary>グループメンバーを置換する。</summary>
    Task<AdminGroupDetailDto> SetGroupMembersAsync(
        Guid callerPrincipalId,
        Guid groupId,
        SetAdminGroupMembersRequest request,
        CancellationToken cancellationToken);

    /// <summary>グループ権限を置換する。</summary>
    Task<AdminGroupDetailDto> SetGroupPermissionsAsync(
        Guid callerPrincipalId,
        Guid groupId,
        SetAdminGroupPermissionsRequest request,
        CancellationToken cancellationToken);

    /// <summary>API キーを一覧する（平文は含まない）。</summary>
    Task<IReadOnlyList<AdminApiKeyListItemDto>> ListApiKeysAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken);

    /// <summary>API キーを発行する（平文は応答でのみ返す）。</summary>
    Task<CreatedAdminApiKeyDto> CreateApiKeyAsync(
        Guid callerPrincipalId,
        CreateAdminApiKeyRequest request,
        CancellationToken cancellationToken);

    /// <summary>API キーを失効させる（Principal を無効化）。</summary>
    Task RevokeApiKeyAsync(
        Guid callerPrincipalId,
        Guid apiKeyId,
        CancellationToken cancellationToken);

    /// <summary>テナント内 ServiceAccount を一覧する（平文キーなし）。</summary>
    Task<IReadOnlyList<AdminServiceAccountListItemDto>> ListServiceAccountsAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken);

    /// <summary>資格のない ServiceAccount を作成する（API キーは発行しない）。</summary>
    Task<AdminServiceAccountListItemDto> CreateServiceAccountAsync(
        Guid callerPrincipalId,
        CreateAdminServiceAccountRequest request,
        CancellationToken cancellationToken);

    /// <summary>ServiceAccount 詳細を返す。</summary>
    Task<AdminServiceAccountListItemDto> GetServiceAccountAsync(
        Guid callerPrincipalId,
        Guid serviceAccountId,
        CancellationToken cancellationToken);

    /// <summary>ServiceAccount の有効・表示名・所属グループを更新する。</summary>
    Task<AdminServiceAccountListItemDto> UpdateServiceAccountAsync(
        Guid callerPrincipalId,
        Guid serviceAccountId,
        UpdateAdminServiceAccountRequest request,
        CancellationToken cancellationToken);
}

/// <inheritdoc />
internal sealed class TenantAdministrationService : ITenantAdministrationService
{
    private const string UnauthorizedCode = "UNAUTHORIZED";
    private const string ForbiddenCode = "FORBIDDEN";
    private const string PrincipalNotFoundMessage = "Principal not found.";
    private static readonly JsonSerializerOptions AllowedScopesJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<CoreDbContext> _dbFactory;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ITenantAdminAuthorization _tenantAdminAuthorization;
    private readonly PasswordCredentialService _passwordCredentialService;
    private readonly IIdGenerator _idGenerator;
    private readonly ILogger<TenantAdministrationService> _logger;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public TenantAdministrationService(
        IDbContextFactory<CoreDbContext> dbFactory,
        ITenantContextAccessor tenantContext,
        ITenantAdminAuthorization tenantAdminAuthorization,
        PasswordCredentialService passwordCredentialService,
        IIdGenerator idGenerator,
        ILogger<TenantAdministrationService> logger)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
        _tenantAdminAuthorization = tenantAdminAuthorization;
        _passwordCredentialService = passwordCredentialService;
        _idGenerator = idGenerator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PermissionDefinitionDto>> ListPermissionsAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.PermissionDefinitions
            .AsNoTracking()
            .OrderBy(p => p.PermissionKey)
            .Select(p => new PermissionDefinitionDto
            {
                PermissionKey = p.PermissionKey,
                DisplayLabel = p.DisplayLabel,
                DisplayKey = p.DisplayKey,
                IsSystem = p.IsSystem,
                IsDeprecated = p.IsDeprecated
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminUserListItemDto>> ListUsersAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var userRows = await (
            from user in db.Users.AsNoTracking()
            join link in db.UserPrincipals.AsNoTracking() on user.UserId equals link.UserId
            join principal in db.Principals.AsNoTracking() on link.PrincipalId equals principal.PrincipalId into principals
            from principal in principals.DefaultIfEmpty()
            orderby user.Username
            select new
            {
                user.UserId,
                user.Username,
                user.Email,
                user.IsTenantAdmin,
                user.IsActive,
                user.CreatedAt,
                link.PrincipalId,
                PrincipalDisplayName = principal != null ? principal.DisplayName : null
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (userRows.Count == 0)
            return Array.Empty<AdminUserListItemDto>();

        var userIds = userRows.Select(row => row.UserId).ToList();
        var memberships = await db.UserGroupMembers
            .AsNoTracking()
            .Where(m => userIds.Contains(m.UserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var groupsByUser = memberships
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.GroupId).ToList());

        return userRows
            .Select(row =>
            {
                groupsByUser.TryGetValue(row.UserId, out var groupIds);
                return new AdminUserListItemDto
                {
                    UserId = row.UserId,
                    PrincipalId = row.PrincipalId,
                    Username = row.Username,
                    Email = row.Email,
                    DisplayName = row.PrincipalDisplayName ?? row.Username,
                    IsTenantAdmin = row.IsTenantAdmin,
                    IsActive = row.IsActive,
                    GroupIds = groupIds is null ? Array.Empty<Guid>() : groupIds,
                    CreatedAt = row.CreatedAt
                };
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AdminUserListItemDto> CreateUserAsync(
        Guid callerPrincipalId,
        CreateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        var tenantId = RequireTenantId();
        var username = request.Username.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var usernameTaken = await db.Users
            .AnyAsync(u => u.TenantId == tenantId && u.Username == username, cancellationToken)
            .ConfigureAwait(false);
        if (usernameTaken)
            throw new ArgumentException($"User '{username}' already exists.", nameof(request));

        if (email is not null)
        {
            var emailTaken = await db.Users
                .AnyAsync(u => u.TenantId == tenantId && u.Email == email, cancellationToken)
                .ConfigureAwait(false);
            if (emailTaken)
                throw new ArgumentException($"Email '{email}' already exists.", nameof(request));
        }

        var groupIds = request.GroupIds ?? Array.Empty<Guid>();
        if (groupIds.Count > 0)
            await EnsureGroupIdsExistAsync(db, groupIds, cancellationToken).ConfigureAwait(false);

        var userId = _idGenerator.NewSequentialGuid();
        var principalId = _idGenerator.NewSequentialGuid();
        var now = DateTime.UtcNow;
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? username : request.DisplayName.Trim();

        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = tenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.User,
            DisplayName = displayName,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Users.Add(new UserRow
        {
            UserId = userId,
            TenantId = tenantId,
            Username = username,
            Email = email,
            PasswordHash = _passwordCredentialService.HashPassword(request.Password),
            IsTenantAdmin = request.IsTenantAdmin ?? false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.UserPrincipals.Add(new UserPrincipalRow { UserId = userId, PrincipalId = principalId });
        foreach (var groupId in groupIds)
            db.UserGroupMembers.Add(new UserGroupMemberRow { UserId = userId, GroupId = groupId });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminUserListItemDto
        {
            UserId = userId,
            PrincipalId = principalId,
            Username = username,
            Email = email,
            DisplayName = displayName,
            IsTenantAdmin = request.IsTenantAdmin ?? false,
            IsActive = true,
            GroupIds = groupIds,
            CreatedAt = now
        };
    }

    /// <inheritdoc />
    public async Task<AdminUserListItemDto> UpdateUserAsync(
        Guid callerPrincipalId,
        Guid userId,
        UpdateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            throw new NotFoundException("User not found.");

        var link = await db.UserPrincipals
            .FirstOrDefaultAsync(up => up.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (link is null)
            throw new NotFoundException("User principal link not found.");

        var principal = await db.Principals
            .FirstOrDefaultAsync(p => p.PrincipalId == link.PrincipalId, cancellationToken)
            .ConfigureAwait(false);
        if (principal is null)
            throw new NotFoundException(PrincipalNotFoundMessage);

        var now = DateTime.UtcNow;
        if (request.IsTenantAdmin is { } isTenantAdmin)
            user.IsTenantAdmin = isTenantAdmin;
        if (request.IsActive is { } isActive)
        {
            user.IsActive = isActive;
            user.DisabledAt = isActive ? null : now;
            principal.IsActive = isActive;
            principal.DisabledAt = isActive ? null : now;
        }

        user.UpdatedAt = now;
        principal.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var groupIds = await db.UserGroupMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.GroupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AdminUserListItemDto
        {
            UserId = user.UserId,
            PrincipalId = link.PrincipalId,
            Username = user.Username,
            Email = user.Email,
            DisplayName = principal.DisplayName,
            IsTenantAdmin = user.IsTenantAdmin,
            IsActive = user.IsActive,
            GroupIds = groupIds,
            CreatedAt = user.CreatedAt
        };
    }

    /// <inheritdoc />
    public async Task UpdateUserPasswordAsync(
        Guid callerPrincipalId,
        Guid userId,
        UpdateAdminUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            throw new NotFoundException("User not found.");

        user.PasswordHash = _passwordCredentialService.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.UserPasswordUpdatedByAdmin(callerPrincipalId, userId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminGroupListItemDto>> ListGroupsAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Groups
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new AdminGroupListItemDto
            {
                GroupId = g.GroupId,
                Name = g.Name,
                IsSystem = g.IsSystem,
                MemberCount = db.UserGroupMembers.Count(m => m.GroupId == g.GroupId),
                PermissionCount = db.GroupPermissions.Count(gp => gp.GroupId == g.GroupId),
                UpdatedAt = g.UpdatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdminGroupDetailDto> CreateGroupAsync(
        Guid callerPrincipalId,
        CreateAdminGroupRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        var tenantId = RequireTenantId();
        var name = request.Name.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var nameTaken = await db.Groups.AnyAsync(g => g.Name == name, cancellationToken).ConfigureAwait(false);
        if (nameTaken)
            throw new ArgumentException($"Group '{name}' already exists.", nameof(request));

        var now = DateTime.UtcNow;
        var groupId = _idGenerator.NewSequentialGuid();
        db.Groups.Add(new GroupRow
        {
            GroupId = groupId,
            TenantId = tenantId,
            Name = name,
            IsSystem = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminGroupDetailDto
        {
            GroupId = groupId,
            Name = name,
            IsSystem = false,
            MemberUserIds = Array.Empty<Guid>(),
            ServiceAccountIds = Array.Empty<Guid>(),
            PermissionKeys = Array.Empty<string>()
        };
    }

    /// <inheritdoc />
    public async Task<AdminGroupDetailDto> GetGroupAsync(
        Guid callerPrincipalId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);
        return await LoadGroupDetailAsync(groupId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdminGroupDetailDto> SetGroupMembersAsync(
        Guid callerPrincipalId,
        Guid groupId,
        SetAdminGroupMembersRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        var userIds = request.UserIds ?? Array.Empty<Guid>();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var group = await db.Groups.FirstOrDefaultAsync(g => g.GroupId == groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
            throw new NotFoundException("Group not found.");

        if (userIds.Count > 0)
        {
            var found = await db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (found.Count != userIds.Count)
                throw new ArgumentException("One or more user IDs are invalid.", nameof(request));
        }

        var existing = await db.UserGroupMembers
            .Where(m => m.GroupId == groupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.UserGroupMembers.RemoveRange(existing);
        foreach (var userId in userIds)
            db.UserGroupMembers.Add(new UserGroupMemberRow { UserId = userId, GroupId = groupId });

        group.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await LoadGroupDetailAsync(groupId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdminGroupDetailDto> SetGroupPermissionsAsync(
        Guid callerPrincipalId,
        Guid groupId,
        SetAdminGroupPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        var keys = NormalizeAssignablePermissionKeys(request.PermissionKeys);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var group = await db.Groups.FirstOrDefaultAsync(g => g.GroupId == groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
            throw new NotFoundException("Group not found.");

        if (keys.Count > 0)
        {
            var catalog = await db.PermissionDefinitions
                .AsNoTracking()
                .Select(p => p.PermissionKey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var catalogSet = catalog.ToHashSet(StringComparer.Ordinal);
            var unknown = keys.Where(k => !catalogSet.Contains(k)).ToList();
            if (unknown.Count > 0)
                throw new ArgumentException($"Unknown permission keys: {string.Join(", ", unknown)}.", nameof(request));
        }

        var existing = await db.GroupPermissions
            .Where(gp => gp.GroupId == groupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.GroupPermissions.RemoveRange(existing);
        foreach (var key in keys)
            db.GroupPermissions.Add(new GroupPermissionRow { GroupId = groupId, PermissionKey = key });

        group.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await LoadGroupDetailAsync(groupId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminApiKeyListItemDto>> ListApiKeysAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await (
            from key in db.ApiKeys.AsNoTracking()
            join principal in db.Principals.AsNoTracking() on key.PrincipalId equals principal.PrincipalId
            orderby key.CreatedAt descending
            select new
            {
                key.ApiKeyId,
                key.KeyPrefix,
                key.AllowedScopesJson,
                key.ExpiresAt,
                key.LastUsedAt,
                key.CreatedAt,
                principal.DisplayName,
                principal.IsActive
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(row => new AdminApiKeyListItemDto
            {
                ApiKeyId = row.ApiKeyId,
                Name = row.DisplayName,
                KeyPrefix = row.KeyPrefix,
                AllowedScopes = ParseAllowedScopesJson(row.AllowedScopesJson),
                ExpiresAt = row.ExpiresAt,
                LastUsedAt = row.LastUsedAt,
                CreatedAt = row.CreatedAt,
                IsActive = row.IsActive
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CreatedAdminApiKeyDto> CreateApiKeyAsync(
        Guid callerPrincipalId,
        CreateAdminApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        var tenantId = RequireTenantId();
        var name = request.Name.Trim();
        var allowedScopes = await NormalizeApiKeyAllowedScopesAsync(request.AllowedScopes, cancellationToken)
            .ConfigureAwait(false);
        var plainKey = PasswordCredentialService.GeneratePlainApiKey();
        var apiKeyId = _idGenerator.NewSequentialGuid();
        var principalId = _idGenerator.NewSequentialGuid();
        var serviceAccountId = _idGenerator.NewSequentialGuid();
        var groupId = _idGenerator.NewSequentialGuid();
        var now = DateTime.UtcNow;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = tenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.ServiceAccount,
            DisplayName = name,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.ServiceAccounts.Add(new ServiceAccountRow
        {
            ServiceAccountId = serviceAccountId,
            TenantId = tenantId,
            PrincipalId = principalId,
            Name = name,
            CreatedAt = now
        });
        db.Groups.Add(new GroupRow
        {
            GroupId = groupId,
            TenantId = tenantId,
            Name = $"api-key-{apiKeyId:N}",
            IsSystem = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        foreach (var scope in allowedScopes)
            db.GroupPermissions.Add(new GroupPermissionRow { GroupId = groupId, PermissionKey = scope });
        db.ServiceAccountGroupMembers.Add(new ServiceAccountGroupMemberRow
        {
            ServiceAccountId = serviceAccountId,
            GroupId = groupId
        });
        db.ApiKeys.Add(new ApiKeyRow
        {
            ApiKeyId = apiKeyId,
            TenantId = tenantId,
            PrincipalId = principalId,
            KeyPrefix = PasswordCredentialService.ApiKeyPrefix(plainKey),
            KeyHash = PasswordCredentialService.HashApiKey(plainKey),
            AllowedScopesJson = SerializeAllowedScopesJson(allowedScopes),
            ExpiresAt = request.ExpiresAt,
            CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreatedAdminApiKeyDto
        {
            ApiKeyId = apiKeyId,
            Name = name,
            KeyPrefix = PasswordCredentialService.ApiKeyPrefix(plainKey),
            PlainKey = plainKey,
            AllowedScopes = allowedScopes,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = now
        };
    }

    /// <inheritdoc />
    public async Task RevokeApiKeyAsync(
        Guid callerPrincipalId,
        Guid apiKeyId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var apiKey = await db.ApiKeys.FirstOrDefaultAsync(k => k.ApiKeyId == apiKeyId, cancellationToken).ConfigureAwait(false);
        if (apiKey is null)
            throw new NotFoundException("API key not found.");

        var principal = await db.Principals
            .FirstOrDefaultAsync(p => p.PrincipalId == apiKey.PrincipalId, cancellationToken)
            .ConfigureAwait(false);
        if (principal is null)
            throw new NotFoundException(PrincipalNotFoundMessage);

        var now = DateTime.UtcNow;
        principal.IsActive = false;
        principal.DisabledAt = now;
        principal.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminServiceAccountListItemDto>> ListServiceAccountsAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await (
            from account in db.ServiceAccounts.AsNoTracking()
            join principal in db.Principals.AsNoTracking() on account.PrincipalId equals principal.PrincipalId
            orderby account.Name
            select new { account, principal }).ToListAsync(cancellationToken).ConfigureAwait(false);

        var serviceAccountIds = rows.Select(row => row.account.ServiceAccountId).ToList();
        var principalIds = rows.Select(row => row.account.PrincipalId).ToList();
        var memberships = await db.ServiceAccountGroupMembers
            .AsNoTracking()
            .Where(member => serviceAccountIds.Contains(member.ServiceAccountId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var keyedPrincipalIds = await db.ApiKeys
            .AsNoTracking()
            .Where(key => principalIds.Contains(key.PrincipalId))
            .Select(key => key.PrincipalId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var keyedSet = keyedPrincipalIds.ToHashSet();
        var groupsByAccount = memberships
            .GroupBy(member => member.ServiceAccountId)
            .ToDictionary(group => group.Key, group => group.Select(member => member.GroupId).ToList() as IReadOnlyList<Guid>);

        return rows.Select(row => new AdminServiceAccountListItemDto
        {
            ServiceAccountId = row.account.ServiceAccountId,
            PrincipalId = row.account.PrincipalId,
            Name = row.account.Name,
            IsActive = row.principal.IsActive,
            HasApiKey = keyedSet.Contains(row.account.PrincipalId),
            GroupIds = groupsByAccount.GetValueOrDefault(row.account.ServiceAccountId, Array.Empty<Guid>()),
            CreatedAt = row.account.CreatedAt
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<AdminServiceAccountListItemDto> CreateServiceAccountAsync(
        Guid callerPrincipalId,
        CreateAdminServiceAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        var tenantId = RequireTenantId();
        var name = request.Name.Trim();
        var groupIds = DistinctGroupIds(request.GroupIds);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await EnsureGroupIdsExistAsync(db, groupIds, cancellationToken).ConfigureAwait(false);

        var principalId = _idGenerator.NewSequentialGuid();
        var serviceAccountId = _idGenerator.NewSequentialGuid();
        var now = DateTime.UtcNow;
        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = tenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.ServiceAccount,
            DisplayName = name,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.ServiceAccounts.Add(new ServiceAccountRow
        {
            ServiceAccountId = serviceAccountId,
            TenantId = tenantId,
            PrincipalId = principalId,
            Name = name,
            CreatedAt = now
        });
        foreach (var groupId in groupIds)
        {
            db.ServiceAccountGroupMembers.Add(new ServiceAccountGroupMemberRow
            {
                ServiceAccountId = serviceAccountId,
                GroupId = groupId
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminServiceAccountListItemDto
        {
            ServiceAccountId = serviceAccountId,
            PrincipalId = principalId,
            Name = name,
            IsActive = true,
            HasApiKey = false,
            GroupIds = groupIds,
            CreatedAt = now
        };
    }

    /// <inheritdoc />
    public async Task<AdminServiceAccountListItemDto> GetServiceAccountAsync(
        Guid callerPrincipalId,
        Guid serviceAccountId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await LoadServiceAccountDtoAsync(db, serviceAccountId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdminServiceAccountListItemDto> UpdateServiceAccountAsync(
        Guid callerPrincipalId,
        Guid serviceAccountId,
        UpdateAdminServiceAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var account = await db.ServiceAccounts
            .FirstOrDefaultAsync(row => row.ServiceAccountId == serviceAccountId, cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
            throw new NotFoundException("Service account not found.");

        var principal = await db.Principals
            .FirstOrDefaultAsync(row => row.PrincipalId == account.PrincipalId, cancellationToken)
            .ConfigureAwait(false);
        if (principal is null)
            throw new NotFoundException(PrincipalNotFoundMessage);

        var now = DateTime.UtcNow;
        if (request.DisplayName is { } displayName)
        {
            var trimmed = displayName.Trim();
            account.Name = trimmed;
            principal.DisplayName = trimmed;
        }

        if (request.IsActive is { } isActive)
        {
            principal.IsActive = isActive;
            principal.DisabledAt = isActive ? null : now;
        }

        if (request.GroupIds is not null)
        {
            var groupIds = DistinctGroupIds(request.GroupIds);
            await EnsureGroupIdsExistAsync(db, groupIds, cancellationToken).ConfigureAwait(false);
            var existing = await db.ServiceAccountGroupMembers
                .Where(member => member.ServiceAccountId == serviceAccountId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            db.ServiceAccountGroupMembers.RemoveRange(existing);
            foreach (var groupId in groupIds)
            {
                db.ServiceAccountGroupMembers.Add(new ServiceAccountGroupMemberRow
                {
                    ServiceAccountId = serviceAccountId,
                    GroupId = groupId
                });
            }
        }

        principal.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await LoadServiceAccountDtoAsync(db, serviceAccountId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<string>> NormalizeApiKeyAllowedScopesAsync(
        IReadOnlyList<string>? scopes,
        CancellationToken cancellationToken)
    {
        if (scopes is null || scopes.Count == 0)
            throw new ArgumentException("allowedScopes must contain at least one scope.");

        var normalized = scopes
            .Select(scope => scope.Trim())
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Where(scope => !string.Equals(scope, WellKnownPermissionKeys.TenantAdmin, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (normalized.Count == 0)
            throw new ArgumentException("allowedScopes must contain at least one assignable scope.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var catalog = await db.PermissionDefinitions
            .AsNoTracking()
            .Select(p => p.PermissionKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var catalogSet = catalog.ToHashSet(StringComparer.Ordinal);
        var unknown = normalized.Where(scope => !catalogSet.Contains(scope)).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException($"Unknown permission keys: {string.Join(", ", unknown)}.");

        return normalized;
    }

    private static string SerializeAllowedScopesJson(IReadOnlyList<string> scopes) =>
        JsonSerializer.Serialize(scopes, AllowedScopesJsonOptions);

    private static string[] ParseAllowedScopesJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<string[]>(json, AllowedScopesJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<AdminGroupDetailDto> LoadGroupDetailAsync(Guid groupId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.GroupId == groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
            throw new NotFoundException("Group not found.");

        var memberUserIds = await db.UserGroupMembers
            .AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var permissionKeys = await db.GroupPermissions
            .AsNoTracking()
            .Where(gp => gp.GroupId == groupId)
            .Select(gp => gp.PermissionKey)
            .OrderBy(k => k)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var serviceAccountIds = await db.ServiceAccountGroupMembers
            .AsNoTracking()
            .Where(member => member.GroupId == groupId)
            .Select(member => member.ServiceAccountId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AdminGroupDetailDto
        {
            GroupId = group.GroupId,
            Name = group.Name,
            IsSystem = group.IsSystem,
            MemberUserIds = memberUserIds,
            ServiceAccountIds = serviceAccountIds,
            PermissionKeys = permissionKeys
        };
    }

    /// <summary>ServiceAccount 1 件を読み取り DTO にする。</summary>
    /// <param name="db">テナントフィルタ済みコンテキスト。</param>
    /// <param name="serviceAccountId">対象 ID。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>一覧と同じ形の DTO。</returns>
    /// <exception cref="NotFoundException">行または Principal が無いとき。</exception>
    private static async Task<AdminServiceAccountListItemDto> LoadServiceAccountDtoAsync(
        CoreDbContext db,
        Guid serviceAccountId,
        CancellationToken cancellationToken)
    {
        var account = await db.ServiceAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.ServiceAccountId == serviceAccountId, cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
            throw new NotFoundException("Service account not found.");

        var principal = await db.Principals
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.PrincipalId == account.PrincipalId, cancellationToken)
            .ConfigureAwait(false);
        if (principal is null)
            throw new NotFoundException(PrincipalNotFoundMessage);

        var groupIds = await db.ServiceAccountGroupMembers
            .AsNoTracking()
            .Where(member => member.ServiceAccountId == serviceAccountId)
            .Select(member => member.GroupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var hasApiKey = await db.ApiKeys
            .AsNoTracking()
            .AnyAsync(key => key.PrincipalId == account.PrincipalId, cancellationToken)
            .ConfigureAwait(false);

        return new AdminServiceAccountListItemDto
        {
            ServiceAccountId = account.ServiceAccountId,
            PrincipalId = account.PrincipalId,
            Name = account.Name,
            IsActive = principal.IsActive,
            HasApiKey = hasApiKey,
            GroupIds = groupIds,
            CreatedAt = account.CreatedAt
        };
    }

    /// <summary>空を拒否し、重複グループ ID を除く。</summary>
    /// <param name="groupIds">要求のグループ ID。</param>
    /// <returns>1 件以上の一意 ID。</returns>
    /// <exception cref="ArgumentException">空のとき。</exception>
    private static List<Guid> DistinctGroupIds(IReadOnlyList<Guid>? groupIds)
    {
        var distinct = (groupIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (distinct.Count == 0)
            throw new ArgumentException("groupIds must contain at least one group.");
        return distinct;
    }

    private static IReadOnlyList<string> NormalizeAssignablePermissionKeys(IReadOnlyList<string>? keys)
    {
        if (keys is null || keys.Count == 0)
            return Array.Empty<string>();

        return keys
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Where(k => !string.Equals(k, WellKnownPermissionKeys.TenantAdmin, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static async Task EnsureGroupIdsExistAsync(
        CoreDbContext db,
        IReadOnlyList<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        var found = await db.Groups
            .AsNoTracking()
            .Where(g => groupIds.Contains(g.GroupId))
            .Select(g => g.GroupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (found.Count != groupIds.Count)
            throw new ArgumentException("One or more group IDs are invalid.");
    }

    private Guid RequireTenantId()
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId is not { } tenantId)
            throw new UnauthorizedException("Authentication required.", UnauthorizedCode);
        return tenantId;
    }

    private async Task EnsureTenantAdminAsync(Guid principalId, CancellationToken cancellationToken)
    {
        if (!await _tenantAdminAuthorization.IsTenantAdminAsync(principalId, cancellationToken).ConfigureAwait(false))
            throw new ForbiddenException("Tenant administrator required.", ForbiddenCode);
    }
}
