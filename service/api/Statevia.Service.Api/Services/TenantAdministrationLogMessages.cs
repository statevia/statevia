namespace Statevia.Service.Api.Services;

/// <summary>
/// <see cref="TenantAdministrationService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class TenantAdministrationLogMessages
{
    /// <summary>テナント管理者が対象ユーザーのパスワードを上書きした。</summary>
    [LoggerMessage(
        EventId = 31001,
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
        EventId = 31002,
        Level = LogLevel.Information,
        Message = "Admin replaced tenant notification SMTP. ActorPrincipalId={actorPrincipalId} PasswordConfigured={passwordConfigured}")]
    public static partial void NotificationSmtpReplaced(
        this ILogger logger,
        Guid actorPrincipalId,
        bool passwordConfigured);

    /// <summary>テナント管理者が Action バインディングを保存した。値と保護文字列は出さない。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="actorPrincipalId">呼び出し主体。</param>
    /// <param name="kind"><c>variable</c> または <c>secret</c>。</param>
    /// <param name="secretValueReplaced">シークレットの暗号文を置換したか。</param>
    [LoggerMessage(
        EventId = 31003,
        Level = LogLevel.Information,
        Message = "Admin saved tenant action binding. ActorPrincipalId={actorPrincipalId} Kind={kind} SecretValueReplaced={secretValueReplaced}")]
    public static partial void ActionBindingSaved(
        this ILogger logger,
        Guid actorPrincipalId,
        string kind,
        bool secretValueReplaced);
}
