using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("Comments ({Nodes?.Count ?? 0})")]
internal sealed record GraphQlCommentConnection(
    [property: JsonPropertyName("nodes")] IReadOnlyList<GraphQlCommentNode>? Nodes
);
