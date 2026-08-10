using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.Storage.InMemory.LoggingExtensions;

internal static partial class SnapshotWriterServiceLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Dispatcher store snapshot writer starting")]
    public static partial void LogSnapshotWriterStarting(this ILogger logger);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Dispatcher store snapshot writer stopping")]
    public static partial void LogSnapshotWriterStopping(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Error writing dispatcher store snapshot")]
    public static partial void LogSnapshotWriteError(this ILogger logger, Exception exception);
}
