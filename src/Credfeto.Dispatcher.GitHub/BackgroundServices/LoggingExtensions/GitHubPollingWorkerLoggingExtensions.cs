using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.BackgroundServices.LoggingExtensions;

internal static partial class GitHubPollingWorkerLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "GitHub polling worker starting")]
    public static partial void LogWorkerStarting(this ILogger logger);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "GitHub polling worker stopping")]
    public static partial void LogWorkerStopping(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Polled {Count} GitHub notifications")]
    public static partial void LogPolledNotifications(this ILogger logger, int count);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Error polling GitHub notifications")]
    public static partial void LogPollingError(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Dispatching notification to Discord: Id={NotificationId}, Repo={Repository}, Title={Title}")]
    public static partial void LogDispatchingNotification(this ILogger logger, string notificationId, string repository, string title);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "Skipping notification for already-closed item: Id={NotificationId}, Repo={Repository}, ItemId={ItemId}, Type={ItemType}")]
    public static partial void LogSkippingClosedItem(this ILogger logger, string notificationId, string repository, int itemId, string itemType);
}
