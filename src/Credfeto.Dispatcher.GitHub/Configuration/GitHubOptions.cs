using System.Collections.Generic;

namespace Credfeto.Dispatcher.GitHub.Configuration;

public sealed class GitHubOptions
{
    public string Token { get; init; } = string.Empty;

    public int PollIntervalSeconds { get; init; } = 60;

    public GitHubFilterOptions Filter { get; init; } = new();
}

public sealed class GitHubFilterOptions
{
    public IReadOnlyList<string> Reasons { get; init; } = [];

    public string LabelFilter { get; init; } = string.Empty;
}
