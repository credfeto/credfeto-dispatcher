using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.Configuration;

[DebuggerDisplay("Token: {Token}, ApiBaseUrl: {ApiBaseUrl}, PollIntervalSeconds: {PollIntervalSeconds}")]
public sealed class GitHubOptions
{
    public string Token { get; set; } = string.Empty;

    public Uri ApiBaseUrl { get; set; } = new("https://api.github.com/");

    public int PollIntervalSeconds { get; set; } = 60;

    public GitHubFilterOptions Filter { get; set; } = new();

    public GitHubScanOptions Scan { get; set; } = new();
}
