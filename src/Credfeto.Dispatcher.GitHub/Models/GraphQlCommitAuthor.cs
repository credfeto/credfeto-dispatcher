using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{User}")]
internal sealed record GraphQlCommitAuthor([property: JsonPropertyName("user")] GraphQlActor? User);
