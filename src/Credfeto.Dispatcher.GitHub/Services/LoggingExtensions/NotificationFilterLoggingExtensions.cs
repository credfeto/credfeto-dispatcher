using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;

internal static partial class NotificationFilterLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Notification {NotificationId} passed filter: reason={Reason}, repo={Repository}")]
    public static partial void LogNotificationPassed(this ILogger logger, string notificationId, string reason, string repository);

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Notification {NotificationId} dropped by reason filter: reason={Reason} not in allowed list")]
    public static partial void LogNotificationDroppedReason(this ILogger logger, string notificationId, string reason);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Notification {NotificationId} dropped by owner filter: owner={Owner} not in allowed owners")]
    public static partial void LogNotificationDroppedOwner(this ILogger logger, string notificationId, string owner);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Notification {NotificationId} dropped by excluded repo filter: repo={Repository} is excluded")]
    public static partial void LogNotificationDroppedExcludedRepo(this ILogger logger, string notificationId, string repository);
}
