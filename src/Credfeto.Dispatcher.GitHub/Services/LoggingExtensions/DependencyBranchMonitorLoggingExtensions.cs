using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;

internal static partial class DependencyBranchMonitorLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Dependency branch {action}: {repo}:{branch}")]
    public static partial void LogDependencyBranchChange(
        this ILogger logger,
        string action,
        string repo,
        string branch
    );

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Dependency branch re-check found PR {repo}#{number} for branch {branch}"
    )]
    public static partial void LogDependencyBranchPullRequestFound(
        this ILogger logger,
        string repo,
        int number,
        string branch
    );

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Dependency branch re-check stopped at {repo}:{branch}: GitHub refused the request (rate limit), the rest wait for the next cycle"
    )]
    public static partial void LogDependencyBranchRecheckRateLimited(this ILogger logger, string repo, string branch);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Dependency branch re-check: {checkedCount} branch(es) checked, {resolvedCount} now have a PR"
    )]
    public static partial void LogDependencyBranchRecheckComplete(
        this ILogger logger,
        int checkedCount,
        int resolvedCount
    );
}
