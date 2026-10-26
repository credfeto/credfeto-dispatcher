using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Repository}#{Number}: {Status} ({Priority})")]
public sealed record PullRequestState(
    string Owner,
    string Repo,
    string Repository,
    int Number,
    string Status,
    WorkPriority Priority,
    bool IsOnHold,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastUpdated,
    DateTimeOffset? WhenClosed,
    ImmutableArray<string> Assignees,
    int CommentCount,
    ReviewDecisionState ReviewDecision,
    int FailedCheckCount,
    ImmutableArray<string> FailedCheckNames,
    string? FailedCheckSha,
    string? Author,
    bool IsAdopted
);
