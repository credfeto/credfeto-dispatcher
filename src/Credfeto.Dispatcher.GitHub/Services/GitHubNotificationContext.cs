using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Services;

[JsonSerializable(typeof(GitHubApiNotification[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class GitHubNotificationContext : JsonSerializerContext;
