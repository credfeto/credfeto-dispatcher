using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;

internal static partial class WorkItemRefresherLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "PR refreshed: {repo}#{number} isOnHold={isOnHold}")]
    public static partial void LogRefreshedPullRequest(this ILogger logger, string repo, int number, bool isOnHold);
}
