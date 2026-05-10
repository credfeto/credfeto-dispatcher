using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{Number}: {Title} ({State})")]
internal sealed record GraphQlPullRequestData(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("isDraft")] bool IsDraft,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("headRefOid")] string HeadRefOid,
    [property: JsonPropertyName("baseRef")] GraphQlRefNode? BaseRef,
    [property: JsonPropertyName("assignees")] GraphQlAssigneeConnection? Assignees,
    [property: JsonPropertyName("labels")] GraphQlLabelConnection? Labels,
    [property: JsonPropertyName("comments")] GraphQlCommentConnection? Comments,
    [property: JsonPropertyName("reviews")] GraphQlReviewConnection? Reviews
);
