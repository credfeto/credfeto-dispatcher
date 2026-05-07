using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.BackgroundServices.LoggingExtensions;

internal static partial class WorkItemScannerServiceLoggingExtensions
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Work item scanner service starting"
    )]
    public static partial void LogScannerStarting(this ILogger logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Work item scanner: no repos configured - scanner will not run. Configure GitHub:Scan:Repos to enable."
    )]
    public static partial void LogNoReposConfigured(this ILogger logger);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Work item scanner service stopping"
    )]
    public static partial void LogScannerStopping(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Error scanning work items")]
    public static partial void LogScanError(this ILogger logger, Exception exception);
}
