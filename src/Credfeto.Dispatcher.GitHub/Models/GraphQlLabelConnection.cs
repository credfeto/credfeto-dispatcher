using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("Labels ({Nodes?.Count ?? 0})")]
internal sealed record GraphQlLabelConnection(
    [property: JsonPropertyName("nodes")] IReadOnlyList<GraphQlLabelNode>? Nodes
);
