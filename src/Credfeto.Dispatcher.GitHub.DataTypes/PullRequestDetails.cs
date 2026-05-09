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
    IReadOnlyList<string> Assignees,
    IReadOnlyList<string> Labels,
    string? Body,
    IReadOnlyList<PullRequestComment> Comments,
    IReadOnlyList<PullRequestReview> Reviews,
    IReadOnlyList<PullRequestRun> Runs,
    IReadOnlyList<LinkedItem> LinkedItems,
    bool? IsUpToDate
);
