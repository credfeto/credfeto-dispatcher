using System.Diagnostics;
using System.Net;

namespace Credfeto.Dispatcher.GitHub.Services;

[DebuggerDisplay(value: "Items={Items?.Length}, NotModified={NotModified}, FailureStatus={FailureStatus}")]
internal readonly record struct PagedETagResult<T>(
    T[]? Items,
    string? NextUrl,
    HttpStatusCode? FailureStatus,
    string? ETag,
    bool NotModified,
    int? PollIntervalSeconds
)
    where T : class;
