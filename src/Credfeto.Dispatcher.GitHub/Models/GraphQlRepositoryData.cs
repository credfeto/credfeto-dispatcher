using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("GraphQL Repository")]
internal sealed record GraphQlRepositoryData(
    [property: JsonPropertyName("pullRequest")] GraphQlPullRequestData? PullRequest
);
