using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.InMemory;

// Flattened form of a single entry from InMemoryDispatcherStore's ETag dictionary
// (Dictionary<string, string>), which isn't JSON-friendly as-is.
[DebuggerDisplay("{Key}: {ETag}")]
internal sealed record PollingStateEntry(string Key, string ETag);
