using System.Text.Json.Serialization;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Microsoft.AspNetCore.Mvc;

namespace Credfeto.Dispatcher.Server;

[JsonSerializable(typeof(PrioritiesResponse))]
[JsonSerializable(typeof(WorkItem))]
[JsonSerializable(typeof(PongDto))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class AppJsonContexts : JsonSerializerContext;
