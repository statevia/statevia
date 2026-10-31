namespace Statevia.Infrastructure.Persistence;

/// <summary>テナントに 1 組だけ保存する通知 SMTP。</summary>
/// <remarks>
/// <para>主キーは <see cref="TenantId"/>。パスワードは保護文字列だけを置き、空ユーザーのときは null。</para>
/// <para>テナント削除は他のテナント外部キーと同じ Restrict。この行が残っているテナントは削除できない。</para>
/// </remarks>
internal sealed class TenantNotificationSmtpRow
{
    /// <summary>オーナーテナント。主キー。</summary>
    public Guid TenantId { get; set; }

    /// <summary>SMTP ホスト。前後空白を除いた 1〜255 文字。</summary>
    public string Host { get; set; } = "";

    /// <summary>SMTP ポート。1〜65535。</summary>
    public int Port { get; set; }

    /// <summary>SMTP ユーザー。空を許す。最大 256 文字。</summary>
    public string SmtpUser { get; set; } = "";

    /// <summary>保護 API で暗号化したパスワード。ユーザーが空のときは null。</summary>
    public string? PasswordProtected { get; set; }

    /// <summary>既定の From。空を許す。</summary>
    public string? DefaultFrom { get; set; }

    /// <summary>最終更新時刻（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
}
