using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;

internal static partial class PullRequestCheckRefresherLoggingExtensions
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "CI refresh: {candidateCount} candidate(s), {unchangedCount} unchanged, {refreshedCount} refreshed, {trackedCount} open PR(s) tracked"
    )]
    public static partial void LogCheckRefreshComplete(
        this ILogger logger,
        int candidateCount,
        int unchangedCount,
        int refreshedCount,
        int trackedCount
    );

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "CI refresh stopped at {repo}#{number}: GitHub refused the request (rate limit), the rest wait for the next cycle"
    )]
    public static partial void LogCheckRefreshRateLimited(this ILogger logger, string repo, int number);
}
