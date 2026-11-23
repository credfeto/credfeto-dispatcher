using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("Action: {Action}")]
internal sealed record ApiEventPayload(
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("number")] int? Number,
    [property: JsonPropertyName("pull_request")] ApiPullRequest? PullRequest,
    [property: JsonPropertyName("issue")] ApiIssue? Issue,
    [property: JsonPropertyName("ref")] string? Ref = null,
    [property: JsonPropertyName("head")] string? Head = null
);
