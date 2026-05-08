using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{State}: {Body}")]
internal sealed record GraphQlReviewNode(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("author")] GraphQlActor? Author,
    [property: JsonPropertyName("url")] string Url
);
