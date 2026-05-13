using System.Text.Json.Serialization;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.Server;

[JsonSerializable(typeof(PrioritiesResponse))]
[JsonSerializable(typeof(WorkItem))]
[JsonSerializable(typeof(PongDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class AppJsonContexts : JsonSerializerContext;
