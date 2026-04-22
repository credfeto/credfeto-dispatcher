using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[JsonSerializable(typeof(ApiNotification[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class NotificationSerializerContext : JsonSerializerContext;
