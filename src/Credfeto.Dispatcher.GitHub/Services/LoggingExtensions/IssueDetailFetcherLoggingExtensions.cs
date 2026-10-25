using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;

internal static partial class IssueDetailFetcherLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to fetch issue from {Url}: {StatusCode}")]
    public static partial void LogIssueFetchFailed(this ILogger logger, string url, HttpStatusCode statusCode);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Could not determine issue number from subject URL {SubjectUrl}"
    )]
    public static partial void LogIssueNumberNotFound(this ILogger logger, Uri subjectUrl);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Refusing to fetch issue from {Url}: request does not target the configured API base address {BaseAddress}"
    )]
    public static partial void LogRequestNotOnApiBaseAddress(this ILogger logger, string url, Uri? baseAddress);
}
