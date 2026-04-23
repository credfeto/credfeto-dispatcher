using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Number}: {Title} ({Status})")]
public sealed record PullRequestDetails(
    int Number,
    string Title,
    string Status,
    Uri HtmlUrl,
    ItemRepository Repository,
    LastNotification LastNotification,
    IReadOnlyList<string> Assignees,
    IReadOnlyList<string> Labels,
    string? CommentBody,
    string? CommentAuthor,
    Uri? CommentUrl,
    string? ReviewState,
    string? ReviewBody,
    string? ReviewAuthor,
    Uri? ReviewUrl,
    string? FailedRunName,
    Uri? FailedRunUrl
);
