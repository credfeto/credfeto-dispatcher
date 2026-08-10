using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Configuration;

[DebuggerDisplay("DirectoryPath: {DirectoryPath}, FileName: {FileName}, IntervalSeconds: {IntervalSeconds}")]
public sealed class SnapshotOptions
{
    // Left blank by default deliberately: DispatcherStoreSnapshotStore falls back to
    // <current working directory>/data when this is blank, rather than baking a container-only
    // path into the option's own default.
    public string DirectoryPath { get; set; } = string.Empty;

    public string FileName { get; set; } = "dispatcher-store-snapshot.json";

    public int IntervalSeconds { get; set; } = 30;
}
