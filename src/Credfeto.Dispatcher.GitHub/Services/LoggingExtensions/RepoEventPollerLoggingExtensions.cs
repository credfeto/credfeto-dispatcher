using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;

internal static partial class RepoEventPollerLoggingExtensions
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Debug,
        Message = "Event poll complete: {repoCount} repo feed(s), {ownerCount} owner feed(s)"
    )]
    public static partial void LogEventPollComplete(this ILogger logger, int repoCount, int ownerCount);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Feed {feed}: {newCount} new event(s), {upsertedCount} upserted, {skippedSeenCount} skipped as already seen, {pageCount} page(s) read, lastId={lastId}"
    )]
    public static partial void LogFeedProcessed(
        this ILogger logger,
        string feed,
        int newCount,
        int upsertedCount,
        int skippedSeenCount,
        int pageCount,
        long lastId
    );

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "PR event upserted: {repo}#{number} action={action} isOnHold={isOnHold}"
    )]
    public static partial void LogProcessedPullRequestEvent(
        this ILogger logger,
        string repo,
        int number,
        string action,
        bool isOnHold
    );

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Issue event upserted: {repo}#{number} action={action} isOnHold={isOnHold}"
    )]
    public static partial void LogProcessedIssueEvent(
        this ILogger logger,
        string repo,
        int number,
        string action,
        bool isOnHold
    );

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "Feed {feed}: not modified since last poll")]
    public static partial void LogFeedNotModified(this ILogger logger, string feed);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Debug,
        Message = "Feed {feed}: no new events, {skippedSeenCount} skipped as already seen, lastId={lastId}"
    )]
    public static partial void LogFeedNoNewEvents(this ILogger logger, string feed, int skippedSeenCount, long lastId);
}
