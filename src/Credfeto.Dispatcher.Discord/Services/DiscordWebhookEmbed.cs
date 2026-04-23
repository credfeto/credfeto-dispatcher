using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.Discord.Services;

[DebuggerDisplay("{Title}: {Url}")]
internal sealed record DiscordWebhookEmbed(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("color")] int Color,
    [property: JsonPropertyName("fields")] IReadOnlyList<DiscordWebhookField>? Fields
);
