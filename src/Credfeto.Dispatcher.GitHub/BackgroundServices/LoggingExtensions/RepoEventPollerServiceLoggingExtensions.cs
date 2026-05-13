using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.BackgroundServices.LoggingExtensions;

internal static partial class RepoEventPollerServiceLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Repo event poller starting")]
    public static partial void LogEventPollerStarting(this ILogger logger);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Repo event poller stopping")]
    public static partial void LogEventPollerStopping(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Repo event poller error")]
    public static partial void LogEventPollerError(this ILogger logger, Exception exception);
}
