using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Oid}")]
internal sealed record GraphQlGitObjectNode([property: JsonPropertyName("oid")] string Oid);
