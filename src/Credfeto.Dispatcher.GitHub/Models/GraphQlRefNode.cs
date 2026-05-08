using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("Ref → {Target}")]
internal sealed record GraphQlRefNode(
    [property: JsonPropertyName("target")] GraphQlGitObjectNode? Target
);
