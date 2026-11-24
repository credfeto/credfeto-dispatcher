using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

// A tracked dependency branch with no pull request yet, due for another lookup; ETag is the last lookup's.
[DebuggerDisplay("{Repository}:{Branch}")]
public sealed record DependencyBranchRecheck(string Repository, string Branch, string? ETag);
