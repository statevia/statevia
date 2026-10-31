using Statevia.Core.Application.Contracts.Validation;
using System.ComponentModel.DataAnnotations;

namespace Statevia.Service.Api.Contracts.Auth;

/// <summary>ログイン要求。</summary>
public sealed class LoginRequest
{
    /// <summary>外部向けテナントキー。</summary>
    [Required(ErrorMessage = "tenantKey is required")]
    [NotWhitespace(ErrorMessage = "tenantKey is required")]
    public string TenantKey { get; set; } = "";

    /// <summary>テナント内ログインユーザー名。</summary>
    [Required(ErrorMessage = "username is required")]
    [NotWhitespace(ErrorMessage = "username is required")]
    [MaxLength(UsernameConstraints.MaxLength)]
    [RegularExpression(UsernameConstraints.AllowedPattern, ErrorMessage = UsernameConstraints.FormatErrorMessage)]
    public string Username { get; set; } = "";

    /// <summary>平文パスワード。</summary>
    [Required(ErrorMessage = "password is required")]
    [NotWhitespace(ErrorMessage = "password is required")]
    public string Password { get; set; } = "";
}

/// <summary>ログイン成功応答。</summary>
public sealed class LoginResponse
{
    /// <summary>JWT アクセストークン。</summary>
    public string AccessToken { get; set; } = "";

    /// <summary>有効期限（UTC）。</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>テナント内部 ID。</summary>
    public Guid TenantId { get; set; }

    /// <summary>外部キー。</summary>
    public string TenantKey { get; set; } = "";

    /// <summary>Principal ID。</summary>
    public Guid PrincipalId { get; set; }
}

/// <summary>認証済み Principal 情報。</summary>
public sealed class AuthMeResponse
{
    /// <summary>テナント内部 ID。</summary>
    public Guid TenantId { get; set; }

    /// <summary>外部キー。</summary>
    public string TenantKey { get; set; } = "";

    /// <summary>Principal ID。</summary>
    public Guid PrincipalId { get; set; }

    /// <summary>ログインユーザー名。</summary>
    public string Username { get; set; } = "";

    /// <summary>任意の連絡先メール。</summary>
    public string? Email { get; set; }

    /// <summary>テナント管理者か。</summary>
    public bool IsTenantAdmin { get; set; }

    /// <summary>
    /// 所属グループから展開した semantic permission key（昇順）。テナント管理者は catalog の全キー。
    /// </summary>
    /// <remarks>
    /// Studio がボタンの表示を決めるための値。認可の正本は各 API の評価であり、ここを根拠に許可しない。
    /// Project ロールは含まない（<c>GET /v1/projects</c> の <c>role</c>）。
    /// </remarks>
    public IReadOnlyList<string> Permissions { get; set; } = [];
}

/// <summary>本人によるパスワード更新要求。</summary>
public sealed class ChangeOwnPasswordRequest
{
    /// <summary>現行の平文パスワード。</summary>
    [Required(ErrorMessage = "currentPassword is required")]
    [NotWhitespace(ErrorMessage = "currentPassword is required")]
    public string CurrentPassword { get; set; } = "";

    /// <summary>新しい平文パスワード（8〜128 文字、空白なし。記号可）。</summary>
    [Required(ErrorMessage = "newPassword is required")]
    [NotWhitespace(ErrorMessage = "newPassword is required")]
    [MinLength(PasswordConstraints.MinLength, ErrorMessage = PasswordConstraints.FormatErrorMessage)]
    [MaxLength(PasswordConstraints.MaxLength, ErrorMessage = PasswordConstraints.FormatErrorMessage)]
    [RegularExpression(PasswordConstraints.AllowedPattern, ErrorMessage = PasswordConstraints.FormatErrorMessage)]
    public string NewPassword { get; set; } = "";
}
