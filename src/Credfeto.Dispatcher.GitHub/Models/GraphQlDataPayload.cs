using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("GraphQL Data")]
internal sealed record GraphQlDataPayload(
    [property: JsonPropertyName("repository")] GraphQlRepositoryData? Repository
);
