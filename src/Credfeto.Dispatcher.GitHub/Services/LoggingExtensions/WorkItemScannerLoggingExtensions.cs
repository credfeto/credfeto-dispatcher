using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;

internal static partial class WorkItemScannerLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Scanning repo {Repo} for open work items")]
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

    [Conditional("DEBUG")]
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Scanned PR #{Number} in {Repo}: status={Status}")]
    public static partial void LogScannedPullRequest(this ILogger logger, string repo, int number, string status);

    [Conditional("DEBUG")]
    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Scanned issue #{Number} in {Repo}")]
    public static partial void LogScannedIssue(this ILogger logger, string repo, int number);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Work item scan complete")]
    public static partial void LogScanComplete(this ILogger logger);

    [Conditional("DEBUG")]
    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Debug,
        Message = "Repo {Repo} skipped during discovery: archived or disabled"
    )]
    public static partial void LogRepoSkippedInactive(this ILogger logger, string repo);

    [Conditional("DEBUG")]
    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Debug,
        Message = "Repo {Repo} skipped during discovery: no push permission"
    )]
    public static partial void LogRepoSkippedNoPushPermission(this ILogger logger, string repo);

    [Conditional("DEBUG")]
    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Debug,
        Message = "Repo {Repo} skipped during discovery: owner '{Owner}' not in AllowedOwners"
    )]
    public static partial void LogRepoSkippedOwnerFilter(this ILogger logger, string repo, string owner);

    [Conditional("DEBUG")]
    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Debug,
        Message = "Repo {Repo} skipped during discovery: not in AllowedRepos list"
    )]
    public static partial void LogRepoSkippedAllowedRepoFilter(this ILogger logger, string repo);

    [Conditional("DEBUG")]
    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Debug,
        Message = "Repo {Repo} skipped during discovery: present in ExcludedRepos list"
    )]
    public static partial void LogRepoSkippedExcludedRepoFilter(this ILogger logger, string repo);
}
