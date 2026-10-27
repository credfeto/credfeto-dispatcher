using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Database.Rows;

[DebuggerDisplay("{Repository}#{Id}: {Status}")]
internal sealed record IssueStateRow(
    string Repository,
    int Id,
    string Status,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastUpdated,
    DateTimeOffset? WhenClosed,
    int Priority,
    bool IsOnHold,
    int? LinkedPrNumber,
    string? Assignees
);
