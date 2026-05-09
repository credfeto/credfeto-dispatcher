using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Context}")]
internal sealed record ApiStatusCheck([property: JsonPropertyName("context")] string Context);
