using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Database.Rows;

[DebuggerDisplay("{ItemType} {Repository}#{Id}: {Status}")]
internal sealed record WorkItemRow(
    string Repository,
    int Id,
    string ItemType,
    string Status,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastUpdated,
    DateTimeOffset? WhenClosed,
    int Priority,
    bool IsOnHold,
    int? LinkedPrNumber,
    int CommentCount,
    string? ReviewDecision,
    int FailedCheckCount,
    string? FailedCheckNames,
    string? FailedCheckSha,
    string? Author
);
