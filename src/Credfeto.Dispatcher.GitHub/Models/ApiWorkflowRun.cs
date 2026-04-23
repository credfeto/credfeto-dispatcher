using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Name}: {Conclusion}")]
internal sealed record ApiWorkflowRun(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("conclusion")] string? Conclusion,
    [property: JsonPropertyName("html_url")] string HtmlUrl
);
