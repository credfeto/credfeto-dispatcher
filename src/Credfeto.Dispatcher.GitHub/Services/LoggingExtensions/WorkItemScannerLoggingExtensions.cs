using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;

internal static partial class WorkItemScannerLoggingExtensions
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Scanning repo {Repo} for open work items"
    )]
    public static partial void LogScanningRepo(this ILogger logger, string repo);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Discovered {Count} repos with write access to scan"
    )]
    public static partial void LogDiscoveredRepos(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "No repos with write access discovered. Check the token permissions and AllowedOwners/AllowedRepos/ExcludedRepos filter configuration."
    )]
    public static partial void LogNoReposDiscovered(this ILogger logger);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Scanned PR #{Number} in {Repo}: status={Status}"
    )]
    public static partial void LogScannedPullRequest(
        this ILogger logger,
        string repo,
        int number,
        string status
    );

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Scanned issue #{Number} in {Repo}"
    )]
    public static partial void LogScannedIssue(this ILogger logger, string repo, int number);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Failed to fetch page from {Url}"
    )]
    public static partial void LogPageFetchFailed(this ILogger logger, string url);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Work item scan complete")]
    public static partial void LogScanComplete(this ILogger logger);
}
