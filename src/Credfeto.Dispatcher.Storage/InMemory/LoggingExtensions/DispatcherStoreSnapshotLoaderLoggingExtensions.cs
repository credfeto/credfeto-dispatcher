using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.Storage.InMemory.LoggingExtensions;

internal static partial class DispatcherStoreSnapshotLoaderLoggingExtensions
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Loaded dispatcher store snapshot: {PullRequestCount} pull requests, {IssueCount} issues"
    )]
    public static partial void SnapshotLoaded(this ILogger logger, int pullRequestCount, int issueCount);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "No dispatcher store snapshot loaded - starting with an empty store"
    )]
    public static partial void SnapshotNotLoaded(this ILogger logger);
}
