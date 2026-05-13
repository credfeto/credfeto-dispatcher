using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;

internal static partial class GitHubRepoHelperLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to fetch page from {Url}")]
    public static partial void LogPageFetchFailed(this ILogger logger, string url);
}
