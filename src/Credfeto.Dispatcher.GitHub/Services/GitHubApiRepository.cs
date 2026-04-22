using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Services;

[DebuggerDisplay("{FullName}: {HtmlUrl}")]
internal sealed record GitHubApiRepository(
    [property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("html_url")] string HtmlUrl
);
