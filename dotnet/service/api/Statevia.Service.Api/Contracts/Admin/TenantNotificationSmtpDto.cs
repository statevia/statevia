namespace Statevia.Service.Api.Contracts.Admin;

/// <summary>テナント通知 SMTP の参照。パスワードは含まない。</summary>
public sealed class TenantNotificationSmtpDto
{
    /// <summary>保護用途。パスワード列の追加認証データに使う。</summary>
    public const string PasswordPurpose = "notification.smtp.password";

    /// <summary>行が存在するとき true。</summary>
    public bool Configured { get; init; }

    /// <summary>SMTP ホスト。未設定なら null。</summary>
    public string? Host { get; init; }

    /// <summary>SMTP ポート。未設定なら null。</summary>
    public int? Port { get; init; }

    /// <summary>SMTP ユーザー。未設定なら null。</summary>
    public string? User { get; init; }

    /// <summary>既定 From。未設定または空なら null。</summary>
    public string? DefaultFrom { get; init; }

    /// <summary>保護されたパスワードが保存されているとき true。</summary>
    public bool PasswordConfigured { get; init; }
}

/// <summary>テナント通知 SMTP の全体置換。パスワード省略で旧値は残さない。</summary>
public sealed class PutTenantNotificationSmtpRequest
{
    /// <summary>SMTP ホスト。</summary>
    public string Host { get; set; } = "";

    /// <summary>SMTP ポート。1〜65535。省略は受け付けない。</summary>
    public required int Port { get; set; }

    /// <summary>SMTP ユーザー。空ならパスワードも空である必要がある。</summary>
    public string? User { get; set; }

    /// <summary>SMTP パスワード。応答には返さない。前後空白は削らない。</summary>
    public string? Password { get; set; }

    /// <summary>既定 From。省略できる。</summary>
    public string? DefaultFrom { get; set; }
}
