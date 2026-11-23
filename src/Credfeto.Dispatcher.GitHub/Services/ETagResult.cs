using System.Diagnostics;
using System.Net;

namespace Credfeto.Dispatcher.GitHub.Services;

[DebuggerDisplay(value: "Item={Item}, NotModified={NotModified}, FailureStatus={FailureStatus}")]
internal readonly record struct ETagResult<T>(
    T? Item,
    HttpStatusCode? FailureStatus,
    string? ETag,
    bool NotModified,
    int? PollIntervalSeconds
)
    where T : class;
