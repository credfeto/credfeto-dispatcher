using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.Configuration;

[DebuggerDisplay("ScanIntervalSeconds: {ScanIntervalSeconds}")]
public sealed class GitHubScanOptions
{
    public int ScanIntervalSeconds { get; init; } = 3600;
}
