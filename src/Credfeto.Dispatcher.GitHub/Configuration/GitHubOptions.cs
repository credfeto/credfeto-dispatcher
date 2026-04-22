using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.Configuration;

[DebuggerDisplay("Token: {Token}, PollIntervalSeconds: {PollIntervalSeconds}")]
public sealed class GitHubOptions
{
    public string Token { get; init; } = string.Empty;

    public int PollIntervalSeconds { get; init; } = 60;

    public GitHubFilterOptions Filter { get; init; } = new();
}
