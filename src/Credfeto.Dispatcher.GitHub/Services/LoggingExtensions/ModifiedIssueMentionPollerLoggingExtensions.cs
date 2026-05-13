using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;

internal static partial class ModifiedIssueMentionPollerLoggingExtensions
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Found mention of configured user in modified issue #{Number} in {Repo}"
    )]
    public static partial void LogFoundMentionInIssue(this ILogger logger, string repo, int number);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Polling repo {Repo} for modified issues with mentions since {Since}"
    )]
    public static partial void LogPollingRepoSince(this ILogger logger, string repo, string since);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Polled modified issues across all repos, found {Count} mention notifications"
    )]
    public static partial void LogPollComplete(this ILogger logger, int count);
}
