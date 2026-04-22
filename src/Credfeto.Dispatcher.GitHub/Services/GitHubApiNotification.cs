using System;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Services;

[DebuggerDisplay("{Id}: {Reason}")]
internal sealed record GitHubApiNotification(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("subject")] GitHubApiSubject Subject,
    [property: JsonPropertyName("repository")] GitHubApiRepository Repository,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("unread")] bool Unread
);
