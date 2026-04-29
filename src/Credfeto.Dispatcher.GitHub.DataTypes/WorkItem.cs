using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Repository}#{Id}: {ItemType} ({Priority})")]
public sealed record WorkItem(
    string Repository,
    int Id,
    string ItemType,
    WorkPriority Priority,
    DateTimeOffset FirstSeen
);
