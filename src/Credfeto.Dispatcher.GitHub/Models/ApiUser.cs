using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Login}")]
internal sealed record ApiUser([property: JsonPropertyName("login")] string Login);
