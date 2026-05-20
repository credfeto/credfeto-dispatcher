using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Database.Rows;

[DebuggerDisplay("{Key}: {ETag}")]
internal sealed record PollingStateRow(string Key, string ETag);
