using System.Text;
using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Contracts.Admin;

namespace Statevia.Service.Api.Services;

/// <summary>自テナントの通知 SMTP を読み、置換し、消す。</summary>
public interface ITenantNotificationSmtpService
{
    /// <summary>自テナントの通知 SMTP を返す。未設定でも行は作らない。</summary>
    /// <param name="callerPrincipalId">呼び出し主体。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>パスワードを含まない接続情報。</returns>
    Task<TenantNotificationSmtpDto> GetAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken);

    /// <summary>自テナントの通知 SMTP を全体置換する。</summary>
    /// <param name="callerPrincipalId">呼び出し主体。</param>
    /// <param name="request">ホスト、ポート、資格情報、既定 From。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>保存後の参照。パスワードは含まない。</returns>
    Task<TenantNotificationSmtpDto> ReplaceAsync(
        Guid callerPrincipalId,
        PutTenantNotificationSmtpRequest request,
        CancellationToken cancellationToken);

    /// <summary>自テナントの通知 SMTP を消す。行が無くても成功する。</summary>
    /// <param name="callerPrincipalId">呼び出し主体。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    Task DeleteAsync(Guid callerPrincipalId, CancellationToken cancellationToken);
}

/// <summary>テナント通知 SMTP の保存。パスワードは保護 API だけを使う。</summary>
/// <remarks>テナント管理者サービスはコンストラクタ引数の上限に達しているため、SMTP だけ分ける。</remarks>
/// <param name="dbFactory">テナントフィルタ付きの DbContext。</param>
/// <param name="tenantContext">呼び出しテナント。</param>
/// <param name="tenantAdminAuthorization">テナント管理者判定。</param>
/// <param name="secretProtector">SMTP パスワードの可逆保護。</param>
/// <param name="logger">置換ログ。パスワードとホストは出さない。</param>
internal sealed class TenantNotificationSmtpService(
    IDbContextFactory<CoreDbContext> dbFactory,
    ITenantContextAccessor tenantContext,
    ITenantAdminAuthorization tenantAdminAuthorization,
    ITenantSecretProtector secretProtector,
    ILogger<TenantNotificationSmtpService> logger) : ITenantNotificationSmtpService
{
    private const string UnauthorizedCode = "UNAUTHORIZED";
    private const string ForbiddenCode = "FORBIDDEN";

    /// <inheritdoc />
    public async Task<TenantNotificationSmtpDto> GetAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantNotificationSmtps.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(row);
    }

    /// <inheritdoc />
    public async Task<TenantNotificationSmtpDto> ReplaceAsync(
        Guid callerPrincipalId,
        PutTenantNotificationSmtpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);
        var tenantId = RequireTenantId();
        var normalized = Normalize(request);
        var passwordProtected = normalized.User.Length == 0
            ? null
            : secretProtector.Protect(tenantId, TenantNotificationSmtpDto.PasswordPurpose, normalized.Password);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantNotificationSmtps.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            row = new TenantNotificationSmtpRow { TenantId = tenantId };
            db.TenantNotificationSmtps.Add(row);
        }

        row.Host = normalized.Host;
        row.Port = normalized.Port;
        row.SmtpUser = normalized.User;
        row.PasswordProtected = passwordProtected;
        row.DefaultFrom = normalized.DefaultFrom;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.NotificationSmtpReplaced(callerPrincipalId, passwordProtected is not null);
        return ToDto(row);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid callerPrincipalId, CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);
        _ = RequireTenantId();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantNotificationSmtps.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null)
            return;

        db.TenantNotificationSmtps.Remove(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PUT のホスト、ポート、資格情報を契約どおりに正規化する。失敗メッセージにパスワードは出さない。</summary>
    /// <param name="request">置換要求。</param>
    /// <returns>保存する値。</returns>
    private static NormalizedSmtp Normalize(PutTenantNotificationSmtpRequest request)
    {
        var host = request.Host.Trim();
        if (host.Length is < 1 or > 255)
            throw new ArgumentException("SMTP host must be 1 to 255 characters.", nameof(request));

        if (request.Port is < 1 or > 65535)
            throw new ArgumentException("SMTP port must be between 1 and 65535.", nameof(request));

        var user = request.User?.Trim() ?? "";
        if (user.Length > 256)
            throw new ArgumentException("SMTP user must be at most 256 characters.", nameof(request));

        var password = request.Password ?? "";
        if (user.Length == 0)
        {
            if (password.Length != 0)
                throw new ArgumentException("SMTP password must be empty when the user is empty.", nameof(request));
        }
        else if (password.Length == 0 || Encoding.UTF8.GetByteCount(password) > 1024)
        {
            throw new ArgumentException("SMTP password must be 1 to 1024 UTF-8 bytes when a user is set.", nameof(request));
        }

        string? defaultFrom = null;
        if (!string.IsNullOrWhiteSpace(request.DefaultFrom))
        {
            defaultFrom = request.DefaultFrom.Trim();
            if (defaultFrom.Length is < 1 or > 256)
                throw new ArgumentException("SMTP default From must be at most 256 characters.", nameof(request));
        }

        return new NormalizedSmtp(host, request.Port, user, password, defaultFrom);
    }

    /// <summary>行をパスワードなしの応答へ写す。行が無いときは未設定。</summary>
    /// <param name="row">保存行。未設定なら null。</param>
    /// <returns>参照 DTO。</returns>
    private static TenantNotificationSmtpDto ToDto(TenantNotificationSmtpRow? row)
    {
        if (row is null)
            return new TenantNotificationSmtpDto();

        return new TenantNotificationSmtpDto
        {
            Configured = true,
            Host = row.Host,
            Port = row.Port,
            User = row.SmtpUser,
            DefaultFrom = row.DefaultFrom,
            PasswordConfigured = row.PasswordProtected is not null
        };
    }

    /// <summary>認証コンテキストのテナント ID を返す。</summary>
    /// <returns>テナント ID。</returns>
    private Guid RequireTenantId()
    {
        if (tenantContext.IsResolved && tenantContext.TenantId is { } tenantId)
            return tenantId;

        throw new UnauthorizedException("Authentication required.", UnauthorizedCode);
    }

    /// <summary>テナント管理者でなければ拒否する。</summary>
    /// <param name="principalId">呼び出し主体。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    private async Task EnsureTenantAdminAsync(Guid principalId, CancellationToken cancellationToken)
    {
        if (!await tenantAdminAuthorization.IsTenantAdminAsync(principalId, cancellationToken).ConfigureAwait(false))
            throw new ForbiddenException("Tenant administrator required.", ForbiddenCode);
    }

    /// <summary>正規化済みの SMTP 置換。</summary>
    /// <param name="Host">ホスト。</param>
    /// <param name="Port">ポート。</param>
    /// <param name="User">ユーザー。空文字はパスワードなし。</param>
    /// <param name="Password">平文パスワード。空ユーザーのときは空。</param>
    /// <param name="DefaultFrom">既定 From。</param>
    private readonly record struct NormalizedSmtp(
        string Host,
        int Port,
        string User,
        string Password,
        string? DefaultFrom);
}
