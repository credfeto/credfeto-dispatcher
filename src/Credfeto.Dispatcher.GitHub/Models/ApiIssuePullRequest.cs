using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{HtmlUrl}")]
internal sealed record ApiIssuePullRequest([property: JsonPropertyName("html_url")] string HtmlUrl);
