using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

// A dependency branch created (CreateEvent) or deleted (DeleteEvent), at the time the event happened.
[DebuggerDisplay("{Repository}:{Branch} deleted={IsDeleted} at {At}")]
public sealed record DependencyBranchChange(string Repository, string Branch, bool IsDeleted, DateTimeOffset At);
