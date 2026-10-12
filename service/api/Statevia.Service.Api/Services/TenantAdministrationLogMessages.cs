namespace Statevia.Service.Api.Services;

/// <summary>
/// <see cref="TenantAdministrationService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class TenantAdministrationLogMessages
{
    /// <summary>テナント管理者が対象ユーザーのパスワードを上書きした。</summary>
    [LoggerMessage(
        EventId = 4040,
        Level = LogLevel.Information,
        Message = "Admin updated user password. ActorPrincipalId={actorPrincipalId} TargetUserId={targetUserId}")]
    public static partial void UserPasswordUpdatedByAdmin(
        this ILogger logger,
        Guid actorPrincipalId,
        Guid targetUserId);

    /// <summary>テナント管理者が通知 SMTP を置換した。パスワードとホストは出さない。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="actorPrincipalId">呼び出し主体。</param>
    /// <param name="passwordConfigured">保護パスワードを保存したか。</param>
    [LoggerMessage(
        EventId = 4041,
        Level = LogLevel.Information,
        Message = "Admin replaced tenant notification SMTP. ActorPrincipalId={actorPrincipalId} PasswordConfigured={passwordConfigured}")]
    public static partial void NotificationSmtpReplaced(
        this ILogger logger,
        Guid actorPrincipalId,
        bool passwordConfigured);
}
