using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Sha}")]
internal sealed record ApiPullRequestHead([property: JsonPropertyName("sha")] string Sha);
