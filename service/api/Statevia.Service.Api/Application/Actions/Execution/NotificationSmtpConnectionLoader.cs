using Microsoft.EntityFrameworkCore;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Contracts.Admin;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>実行テナントの通知 SMTP を復号してディスパッチ用接続にする。</summary>
internal interface INotificationSmtpConnectionLoader
{
    /// <summary>指定テナントの行を読み、パスワードを復元する。</summary>
    /// <param name="tenantId">実行テナント。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>input に入れない接続。</returns>
    /// <exception cref="InvalidOperationException">行が無い、または復元できない。</exception>
    Task<NotificationSmtpDispatch> LoadAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary><see cref="INotificationSmtpConnectionLoader"/> の永続化実装。</summary>
/// <param name="dbFactory">コア DbContext。</param>
/// <param name="secretProtector">保存時と同じ用途で復元する。</param>
internal sealed class NotificationSmtpConnectionLoader(
    IDbContextFactory<CoreDbContext> dbFactory,
    ITenantSecretProtector secretProtector) : INotificationSmtpConnectionLoader
{
    private const string MissingRowMessage = "Notification SMTP is not configured for the execution tenant.";
    private const string RestoreFailedMessage = "Notification SMTP password could not be restored.";

    /// <inheritdoc />
    public async Task<NotificationSmtpDispatch> LoadAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantNotificationSmtps
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            throw new InvalidOperationException(MissingRowMessage);

        string? password = null;
        if (!string.IsNullOrEmpty(row.SmtpUser))
        {
            if (string.IsNullOrEmpty(row.PasswordProtected))
                throw new InvalidOperationException(RestoreFailedMessage);

            try
            {
                password = secretProtector.Unprotect(
                    tenantId,
                    TenantNotificationSmtpDto.PasswordPurpose,
                    row.PasswordProtected);
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException(RestoreFailedMessage);
            }
        }

        return new NotificationSmtpDispatch(
            row.Host,
            row.Port,
            string.IsNullOrEmpty(row.SmtpUser) ? null : row.SmtpUser,
            password,
            string.IsNullOrWhiteSpace(row.DefaultFrom) ? null : row.DefaultFrom);
    }
}
