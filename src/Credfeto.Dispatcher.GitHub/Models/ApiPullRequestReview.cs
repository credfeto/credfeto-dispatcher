using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{State}: {User.Login}")]
internal sealed record ApiPullRequestReview(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("user")] ApiUser User,
    [property: JsonPropertyName("html_url")] string HtmlUrl
);
