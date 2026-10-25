using Microsoft.Extensions.Logging;

namespace Statevia.Core.Application.Services;

/// <summary>Project メンバー変更の監査ログ（<see cref="LoggerMessageAttribute"/>）。</summary>
internal static partial class ProjectMemberLogMessages
{
    /// <summary>付与またはロール変更。</summary>
    [LoggerMessage(
        EventId = 3301,
        Level = LogLevel.Information,
        Message = "Project member upserted. ActorPrincipalId={actorPrincipalId} ProjectId={projectId} TargetPrincipalId={targetPrincipalId} Role={role}")]
    public static partial void ProjectMemberUpserted(
        this ILogger logger,
        Guid actorPrincipalId,
        Guid projectId,
        Guid targetPrincipalId,
        string role);

    /// <summary>メンバー剥奪。</summary>
    [LoggerMessage(
        EventId = 3302,
        Level = LogLevel.Information,
        Message = "Project member removed. ActorPrincipalId={actorPrincipalId} ProjectId={projectId} TargetPrincipalId={targetPrincipalId}")]
    public static partial void ProjectMemberRemoved(
        this ILogger logger,
        Guid actorPrincipalId,
        Guid projectId,
        Guid targetPrincipalId);
}
