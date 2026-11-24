using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Database.Rows;

[DebuggerDisplay("{Repository}:{Branch} PR={PullRequestNumber} deleted={DeletedAt}")]
internal sealed record DependencyBranchRow(
    string Repository,
    string Branch,
    DateTimeOffset FirstSeen,
    DateTimeOffset? LastChecked,
    DateTimeOffset LastUpdated,
    int? PullRequestNumber,
    DateTimeOffset? DeletedAt,
    string? ETag
);
