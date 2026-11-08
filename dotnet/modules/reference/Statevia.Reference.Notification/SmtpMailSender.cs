using Statevia.Core.Actions.Abstractions.Execution;
using System.Net;
using System.Net.Mail;

namespace Statevia.Reference.Notification;

/// <summary>モジュール内で完結する SMTP 送信。</summary>
internal static class SmtpMailSender
{
    /// <summary>組み立て済みメールを SMTP で送る。</summary>
    /// <remarks>TLS（<c>EnableSsl</c>）は常に有効。ユーザー名が空なら既定の資格情報で接続する。</remarks>
    /// <param name="settings">SMTP の接続先と資格情報。</param>
    /// <param name="message">送るメール。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>送信が終わると完了するタスク。</returns>
    public static async Task SendAsync(
        NotificationSmtpDispatch settings,
        MailMessage message,
        CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = true,
            Credentials = string.IsNullOrWhiteSpace(settings.User)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(settings.User, settings.Password),
        };

        await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
