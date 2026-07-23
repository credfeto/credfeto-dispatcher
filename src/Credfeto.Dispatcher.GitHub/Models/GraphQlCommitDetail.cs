using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("Commit")]
internal sealed record GraphQlCommitDetail([property: JsonPropertyName("author")] GraphQlCommitAuthor? Author);
