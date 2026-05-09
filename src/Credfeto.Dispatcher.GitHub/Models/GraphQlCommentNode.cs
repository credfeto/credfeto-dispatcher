using System;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Body}")]
internal sealed record GraphQlCommentNode(
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("author")] GraphQlActor? Author,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt
);
