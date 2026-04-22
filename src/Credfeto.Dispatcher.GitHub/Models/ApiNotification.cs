using System;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Id}: {Reason}")]
internal sealed record ApiNotification(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("subject")] ApiSubject Subject,
    [property: JsonPropertyName("repository")] ApiRepository Repository,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("unread")] bool Unread
);
