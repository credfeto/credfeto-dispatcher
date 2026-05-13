using System;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Id}: {Type} on {Repo.Name}")]
internal sealed record ApiEvent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("repo")] ApiEventRepo Repo,
    [property: JsonPropertyName("payload")] ApiEventPayload Payload,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt
);
