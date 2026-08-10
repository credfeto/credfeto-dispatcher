using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.InMemory;

// Flattened form of a single entry from InMemoryDispatcherStore's repo dictionary
// (Dictionary<string, bool>), which isn't JSON-friendly as-is.
[DebuggerDisplay("{Repository}: IsActive={IsActive}")]
internal sealed record RepoEntry(string Repository, bool IsActive);
