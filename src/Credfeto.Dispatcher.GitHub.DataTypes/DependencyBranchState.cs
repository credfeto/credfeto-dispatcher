using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Repository}:{Branch} first seen {FirstSeen}")]
public sealed record DependencyBranchState(
    string Owner,
    string Repo,
    string Repository,
    string Branch,
    DateTimeOffset FirstSeen,
    DateTimeOffset? LastChecked
);
