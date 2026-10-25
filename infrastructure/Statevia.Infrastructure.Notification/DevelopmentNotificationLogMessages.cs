namespace Statevia.Infrastructure.Notification;

/// <summary><see cref="DevelopmentNotificationSender"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。</summary>
internal static partial class DevelopmentNotificationLogMessages
{
    [LoggerMessage(
        EventId = 12001,
        Level = LogLevel.Warning,
        Message = "notification action skipped in Development (channel=email toLength={ToLength} subjectLength={SubjectLength})")]
    public static partial void NotificationSkippedInDevelopment(
        ILogger logger,
        int toLength,
        int subjectLength);
}
