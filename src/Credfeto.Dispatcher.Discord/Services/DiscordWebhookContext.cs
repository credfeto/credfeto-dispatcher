using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.Discord.Services;

[JsonSerializable(typeof(DiscordWebhookPayload))]
internal sealed partial class DiscordWebhookContext : JsonSerializerContext;
