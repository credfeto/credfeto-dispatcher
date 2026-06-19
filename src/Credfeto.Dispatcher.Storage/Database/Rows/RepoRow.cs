using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Database.Rows;

[DebuggerDisplay("{Repository}")]
internal sealed record RepoRow(string Repository);
