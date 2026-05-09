using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Checks?.Count ?? 0} required checks")]
internal sealed record ApiRequiredStatusChecks(
    [property: JsonPropertyName("checks")] IReadOnlyList<ApiStatusCheck>? Checks
);
