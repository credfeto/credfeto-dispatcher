using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Number}: {Title} ({State})")]
internal sealed record ApiPullRequest(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("draft")] bool Draft,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("assignees")] IReadOnlyList<ApiUser> Assignees,
    [property: JsonPropertyName("labels")] IReadOnlyList<ApiLabel> Labels,
    [property: JsonPropertyName("head")] ApiPullRequestHead Head
);
