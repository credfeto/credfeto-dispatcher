using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Number}: {Title} ({Status})")]
public sealed record PullRequestDetails(
    int Number,
    string Title,
    string? Body,
    WorkItemStatus Status,
    WorkItemPriority Priority,
    bool OnHold,
    Uri HtmlUrl,
    ItemRepository Repository,
    LastNotification LastNotification,
    IReadOnlyList<string> Assignees,
    IReadOnlyList<string> Labels,
    IReadOnlyList<PullRequestComment> Comments,
    IReadOnlyList<PullRequestReview> Reviews,
    IReadOnlyList<PullRequestRun> Runs,
    IReadOnlyList<LinkedItem> LinkedItems
);
