using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Message}")]
internal sealed record GraphQlError([property: JsonPropertyName("message")] string Message);
