using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.Configuration;

[DebuggerDisplay("Repos: {Repos.Count}, ScanIntervalSeconds: {ScanIntervalSeconds}")]
public sealed class GitHubScanOptions
{
    public IReadOnlyList<string> Repos { get; init; } = [];

    public int ScanIntervalSeconds { get; init; } = 3600;
}
