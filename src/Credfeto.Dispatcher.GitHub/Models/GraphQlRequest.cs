using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("GraphQL Request")]
internal sealed record GraphQlRequest(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("variables")] GraphQlVariables Variables
);
