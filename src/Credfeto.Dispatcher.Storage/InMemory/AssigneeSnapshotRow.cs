using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.InMemory;

[DebuggerDisplay("{Repository}#{Id}: {Logins.Length} assignees")]
internal sealed record AssigneeSnapshotRow(string Repository, int Id, string[] Logins);
