using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("Commits ({Nodes?.Count ?? 0})")]
internal sealed record GraphQlCommitConnection(
    [property: JsonPropertyName("nodes")] IReadOnlyList<GraphQlCommitNode>? Nodes
);
