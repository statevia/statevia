namespace Statevia.Infrastructure.Persistence;

/// <summary><c>tenants</c> 行。</summary>
internal sealed class TenantRow
{
    /// <summary>テナントの内部 ID。作成後は変えない。</summary>
    public Guid TenantId { get; set; }
    /// <summary>外部向けのテナントキー（<c>X-Tenant-Id</c> と JWT で使う）。作成後は変えない。</summary>
    public string TenantKey { get; set; } = "";
    /// <summary>表示名。</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>テナントのライフサイクル（Active / Suspended / Archived）。</summary>
    public TenantLifecycle Lifecycle { get; set; }
    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>更新日時（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary><c>permission_definitions</c> 行。</summary>
internal sealed class PermissionDefinitionRow
{
    /// <summary>権限定義 ID。</summary>
    public Guid PermissionDefinitionId { get; set; }
    /// <summary>権限キー（例: <c>executions.read</c>）。テナント横断で一意。</summary>
    public string PermissionKey { get; set; } = "";
    /// <summary>表示ラベル。</summary>
    public string DisplayLabel { get; set; } = "";
    /// <summary>UI 表示用の i18n 辞書キー。無ければ null。</summary>
    public string? DisplayKey { get; set; }
    /// <summary>所有者種別。無ければ null。</summary>
    public string? OwnerType { get; set; }
    /// <summary>所有者キー。無ければ null。</summary>
    public string? OwnerKey { get; set; }
    /// <summary>システム予約の権限か。</summary>
    public bool IsSystem { get; set; }
    /// <summary>非推奨の権限か。</summary>
    public bool IsDeprecated { get; set; }
    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary><c>principals</c> 行。</summary>
internal sealed class PrincipalRow
{
    /// <summary>Principal ID。</summary>
    public Guid PrincipalId { get; set; }
    /// <summary>所属テナントの内部 ID（<c>tenants.tenant_id</c>）。</summary>
    public Guid TenantId { get; set; }
    /// <summary>Principal の範囲（テナント内かプラットフォーム横断か）。</summary>
    public PrincipalScope PrincipalScope { get; set; }
    /// <summary>Principal の種別（User / ServiceAccount / System）。</summary>
    public PrincipalType PrincipalType { get; set; }
    /// <summary>表示名。</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>システム予約の Principal か。</summary>
    public bool IsSystem { get; set; }
    /// <summary>有効か。false の Principal は認可で拒否する。</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>無効にした日時（UTC）。無効でなければ null。</summary>
    public DateTime? DisabledAt { get; set; }
    /// <summary>論理削除した日時（UTC）。削除していなければ null。</summary>
    public DateTime? DeletedAt { get; set; }
    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>更新日時（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary><c>user_principals</c> 行。</summary>
internal sealed class UserPrincipalRow
{
    /// <summary>Principal ID（<c>principals.principal_id</c>）。</summary>
    public Guid PrincipalId { get; set; }
    /// <summary>ユーザー ID（<c>users.user_id</c>）。User と Principal は 1:1。</summary>
    public Guid UserId { get; set; }
}

/// <summary><c>users</c> 行。</summary>
internal sealed class UserRow
{
    /// <summary>ユーザー ID。</summary>
    public Guid UserId { get; set; }
    /// <summary>所属テナントの内部 ID（<c>tenants.tenant_id</c>）。</summary>
    public Guid TenantId { get; set; }
    /// <summary>ログインに使うユーザー名。テナント内で一意。</summary>
    public string Username { get; set; } = "";
    /// <summary>任意の連絡先メールアドレス。認証には使わない。無ければ null。</summary>
    public string? Email { get; set; }
    /// <summary>パスワードのハッシュ。平文は保存しない。</summary>
    public string PasswordHash { get; set; } = "";
    /// <summary>テナント管理者か。</summary>
    public bool IsTenantAdmin { get; set; }
    /// <summary>プラットフォーム管理者か。</summary>
    public bool IsPlatformAdmin { get; set; }
    /// <summary>有効か。</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>無効にした日時（UTC）。無効でなければ null。</summary>
    public DateTime? DisabledAt { get; set; }
    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>更新日時（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary><c>groups</c> 行。</summary>
internal sealed class GroupRow
{
    /// <summary>グループ ID。</summary>
    public Guid GroupId { get; set; }
    /// <summary>所属テナントの内部 ID（<c>tenants.tenant_id</c>）。</summary>
    public Guid TenantId { get; set; }
    /// <summary>グループ名。テナント内で一意。</summary>
    public string Name { get; set; } = "";
    /// <summary>システム予約のグループか。</summary>
    public bool IsSystem { get; set; }
    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>更新日時（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary><c>group_permissions</c> 行。</summary>
internal sealed class GroupPermissionRow
{
    /// <summary>グループ ID（<c>groups.group_id</c>）。</summary>
    public Guid GroupId { get; set; }
    /// <summary>グループに付与した権限キー（<c>permission_definitions.permission_key</c>）。</summary>
    public string PermissionKey { get; set; } = "";
}

/// <summary><c>user_group_members</c> 行。</summary>
internal sealed class UserGroupMemberRow
{
    /// <summary>ユーザー ID（<c>users.user_id</c>）。</summary>
    public Guid UserId { get; set; }
    /// <summary>所属するグループの ID（<c>groups.group_id</c>）。</summary>
    public Guid GroupId { get; set; }
}

/// <summary><c>service_accounts</c> 行。</summary>
internal sealed class ServiceAccountRow
{
    /// <summary>サービスアカウント ID。</summary>
    public Guid ServiceAccountId { get; set; }
    /// <summary>所属テナントの内部 ID（<c>tenants.tenant_id</c>）。</summary>
    public Guid TenantId { get; set; }
    /// <summary>対応する Principal の ID（<c>principals.principal_id</c>）。1:1 で対応する。</summary>
    public Guid PrincipalId { get; set; }
    /// <summary>表示名。</summary>
    public string Name { get; set; } = "";
    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary><c>service_account_group_members</c> 行。</summary>
internal sealed class ServiceAccountGroupMemberRow
{
    /// <summary>サービスアカウント ID（<c>service_accounts.service_account_id</c>）。</summary>
    public Guid ServiceAccountId { get; set; }
    /// <summary>所属するグループの ID（<c>groups.group_id</c>）。</summary>
    public Guid GroupId { get; set; }
}

/// <summary><c>api_keys</c> 行。</summary>
internal sealed class ApiKeyRow
{
    /// <summary>API キー ID。</summary>
    public Guid ApiKeyId { get; set; }
    /// <summary>所属テナントの内部 ID（<c>tenants.tenant_id</c>）。</summary>
    public Guid TenantId { get; set; }
    /// <summary>API キーで認証する Principal の ID（<c>principals.principal_id</c>）。</summary>
    public Guid PrincipalId { get; set; }
    /// <summary>平文キーの先頭数文字。表示と検索に使う。</summary>
    public string KeyPrefix { get; set; } = "";
    /// <summary>平文キーのハッシュ。平文キーは保存しない。</summary>
    public string KeyHash { get; set; } = "";
    /// <summary>API キーに許可したスコープの JSON 配列。Principal の権限との交差だけが有効になる。</summary>
    public string AllowedScopesJson { get; set; } = "[]";
    /// <summary>有効期限（UTC）。無期限なら null。</summary>
    public DateTime? ExpiresAt { get; set; }
    /// <summary>最終利用日時（UTC）。未使用なら null。</summary>
    public DateTime? LastUsedAt { get; set; }
    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary><c>login_failure_locks</c> 行。テナントキー＋ユーザー名のロック期限。</summary>
/// <remarks>JWT 前の点取得用。HasQueryFilter は載せない。未知主体の行は作らない。</remarks>
internal sealed class LoginFailureLockRow
{
    /// <summary>ログイン要求のテナントキー。</summary>
    public string TenantKey { get; set; } = "";
    /// <summary>ログイン要求のユーザー名。</summary>
    public string Username { get; set; } = "";
    /// <summary>ロック期限。null なら未ロック。</summary>
    public DateTime? LockedUntil { get; set; }
}

/// <summary><c>login_failure_attempts</c> 行。窓内の失敗時刻。</summary>
internal sealed class LoginFailureAttemptRow
{
    /// <summary>ログイン失敗の記録 ID。</summary>
    public Guid AttemptId { get; set; }
    /// <summary>親のロック行のテナントキー。</summary>
    public string TenantKey { get; set; } = "";
    /// <summary>親のロック行のユーザー名。</summary>
    public string Username { get; set; } = "";
    /// <summary>ログインに失敗した日時（UTC）。</summary>
    public DateTime FailedAt { get; set; }
}
