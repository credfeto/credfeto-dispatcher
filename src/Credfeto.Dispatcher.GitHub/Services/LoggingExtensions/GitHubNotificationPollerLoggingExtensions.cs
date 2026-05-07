using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;

internal static partial class GitHubNotificationPollerLoggingExtensions // gitleaks:allow
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Polling GitHub notifications with ETag: {ETag}"
    )]
    public static partial void LogPollingWithETag(this ILogger logger, string eTag);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Polling GitHub notifications (no ETag - first call)"
    )]
    public static partial void LogPollingFirstCall(this ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Poll result: 304 Not Modified - no new notifications"
    )]
    public static partial void LogPollNotModified(this ILogger logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Poll result: {Count} notification(s) received"
    )]
    public static partial void LogPollNotificationsReceived(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Notification received: Id={NotificationId}, Reason={Reason}, Repo={Repository}, Title={Title}"
    )]
    public static partial void LogNotificationReceived(
        this ILogger logger,
        string notificationId,
        string reason,
        string repository,
        string title
    );
}
