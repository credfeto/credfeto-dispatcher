using System.Diagnostics;
using System.Net;

namespace Credfeto.Dispatcher.GitHub.Tests.Helpers;

[DebuggerDisplay("StatusCode={StatusCode}, ETag={ETag}, LinkUrl={LinkUrl}")]
internal sealed record SequencedResponse(HttpStatusCode StatusCode, string? Content, string? ETag, string? LinkUrl);
