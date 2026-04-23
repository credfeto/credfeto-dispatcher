using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Contexts.Count} required contexts")]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by System.Text.Json source generator")]
internal sealed record ApiRequiredStatusChecks(
    [property: JsonPropertyName("contexts")] IReadOnlyList<string> Contexts
);
