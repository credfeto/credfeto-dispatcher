using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("Commit Node")]
internal sealed record GraphQlCommitNode([property: JsonPropertyName("commit")] GraphQlCommitDetail? Commit);
