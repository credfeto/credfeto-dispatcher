using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Name} → {Target}")]
internal sealed record GraphQlRefNode(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("target")] GraphQlGitObjectNode? Target
);
