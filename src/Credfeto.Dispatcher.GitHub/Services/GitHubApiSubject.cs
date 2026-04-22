using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Services;

[DebuggerDisplay("{Type}: {Title}")]
internal sealed record GitHubApiSubject(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("type")] string Type
);
