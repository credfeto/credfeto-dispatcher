using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("GraphQL Response")]
internal sealed record GraphQlResponse(
    [property: JsonPropertyName("data")] GraphQlDataPayload? Data,
    [property: JsonPropertyName("errors")] IReadOnlyList<GraphQlError>? Errors
);
