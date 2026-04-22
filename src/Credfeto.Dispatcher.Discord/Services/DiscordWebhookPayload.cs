using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.Discord.Services;

[DebuggerDisplay("{Content} ({Embeds.Count} embeds)")]
internal sealed record DiscordWebhookPayload(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("embeds")] IReadOnlyList<DiscordWebhookEmbed> Embeds
);
