using System.Collections.Generic;
using System.Text.Json.Serialization;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.Server;

[JsonSerializable(typeof(IReadOnlyList<WorkItem>))]
[JsonSerializable(typeof(WorkItem))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class AppJsonContexts : JsonSerializerContext;
