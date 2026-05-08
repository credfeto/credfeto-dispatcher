using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("Reviews ({Nodes?.Count ?? 0})")]
internal sealed record GraphQlReviewConnection(
    [property: JsonPropertyName("nodes")] IReadOnlyList<GraphQlReviewNode>? Nodes
);
