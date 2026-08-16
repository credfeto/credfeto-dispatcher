using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.BackgroundServices.LoggingExtensions;

internal static partial class StartupGitHubAuthCheckServiceLoggingExtensions
{
    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "GitHub authentication successful")]
    public static partial void LogGitHubAuthenticationSuccessful(this ILogger logger);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Error,
        Message = "GitHub authentication failed with status code {StatusCode}"
    )]
    public static partial void LogGitHubAuthenticationFailed(this ILogger logger, int statusCode);

    [LoggerMessage(EventId = 13, Level = LogLevel.Error, Message = "Startup GitHub auth check was cancelled")]
    public static partial void LogStartupGitHubAuthCheckCancelled(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 14, Level = LogLevel.Error, Message = "Error checking GitHub authentication status")]
    public static partial void LogGitHubAuthCheckError(this ILogger logger, Exception exception);
}
