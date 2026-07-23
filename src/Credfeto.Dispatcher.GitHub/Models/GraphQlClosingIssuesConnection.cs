using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("Closing Issues ({Nodes?.Count ?? 0})")]
internal sealed record GraphQlClosingIssuesConnection(
    [property: JsonPropertyName("nodes")] IReadOnlyList<GraphQlClosingIssueNode>? Nodes
);
