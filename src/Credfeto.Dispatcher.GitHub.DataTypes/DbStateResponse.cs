using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay(
    "PRs: {PullRequests.Count}, Issues: {Issues.Count}, Orphaned branches: {OrphanedDependencyBranches.Count}"
)]
public sealed record DbStateResponse(
    IReadOnlyList<PullRequestState> PullRequests,
    IReadOnlyList<IssueState> Issues,
    DateTimeOffset AsOf,
    IReadOnlyList<DependencyBranchState> OrphanedDependencyBranches
);
