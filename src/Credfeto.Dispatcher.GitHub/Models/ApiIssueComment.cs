using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{User.Login}: {Body}")]
internal sealed record ApiIssueComment(
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("user")] ApiUser User,
    [property: JsonPropertyName("html_url")] string HtmlUrl
);
