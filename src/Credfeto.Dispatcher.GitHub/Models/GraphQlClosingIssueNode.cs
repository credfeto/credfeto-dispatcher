using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("Closing Issue {Number}")]
internal sealed record GraphQlClosingIssueNode(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("labels")] GraphQlLabelConnection? Labels,
    [property: JsonPropertyName("assignees")] GraphQlAssigneeConnection? Assignees
);
