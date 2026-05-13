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

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Feed {feed}: processed {count} new event(s)")]
    public static partial void LogFeedProcessed(this ILogger logger, string feed, int count);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "PR event: {repo}#{number} action={action}")]
    public static partial void LogProcessedPullRequestEvent(
        this ILogger logger,
        string repo,
        int number,
        string action
    );

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Issue event: {repo}#{number} action={action}")]
    public static partial void LogProcessedIssueEvent(this ILogger logger, string repo, int number, string action);
}
