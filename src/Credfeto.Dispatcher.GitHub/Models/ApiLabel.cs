using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Name}")]
internal sealed record ApiLabel([property: JsonPropertyName("name")] string Name);
