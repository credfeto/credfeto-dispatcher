using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Owner}/{Repo}#{Number}")]
internal sealed record GraphQlVariables(
    [property: JsonPropertyName("owner")] string Owner,
    [property: JsonPropertyName("repo")] string Repo,
    [property: JsonPropertyName("number")] int Number
);
