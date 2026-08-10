using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.Storage.InMemory.LoggingExtensions;

internal static partial class DispatcherStoreSnapshotStoreLoggingExtensions
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Error,
        Message = "Failed to save dispatcher store snapshot to {FilePath}"
    )]
    public static partial void SnapshotSaveFailed(this ILogger logger, string filePath, Exception exception);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Failed to load dispatcher store snapshot from {FilePath} - starting with an empty store"
    )]
    public static partial void SnapshotLoadFailed(this ILogger logger, string filePath, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Failed to delete temporary snapshot file {FilePath}"
    )]
    public static partial void SnapshotTempFileCleanupFailed(this ILogger logger, string filePath, Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Dispatcher store snapshot at {FilePath} is missing one or more expected fields - starting with an empty store"
    )]
    public static partial void SnapshotLoadIncomplete(this ILogger logger, string filePath);
}
