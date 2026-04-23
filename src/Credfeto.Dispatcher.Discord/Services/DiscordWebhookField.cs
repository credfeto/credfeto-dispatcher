using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.Discord.Services;

[DebuggerDisplay("{Name}: {Value}")]
internal sealed record DiscordWebhookField(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("inline")] bool Inline
);
