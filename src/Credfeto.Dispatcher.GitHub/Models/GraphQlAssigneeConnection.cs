using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("Assignees ({Nodes?.Count ?? 0})")]
internal sealed record GraphQlAssigneeConnection(
    [property: JsonPropertyName("nodes")] IReadOnlyList<GraphQlActor>? Nodes
);
