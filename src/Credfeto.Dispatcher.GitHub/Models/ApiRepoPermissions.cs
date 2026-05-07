using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("Push={Push}")]
internal sealed record ApiRepoPermissions([property: JsonPropertyName("push")] bool Push);
