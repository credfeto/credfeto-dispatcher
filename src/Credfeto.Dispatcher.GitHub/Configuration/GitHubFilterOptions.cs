using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.Configuration;

[DebuggerDisplay("LabelFilter: {LabelFilter.Count}, MaxIssues: {MaxIssues}")]
public sealed class GitHubFilterOptions
{
    public IReadOnlyList<string> LabelFilter { get; set; } = [];

    public IReadOnlyList<string> NoWorkFilter { get; set; } = [];

    public IReadOnlyList<string> AllowedOwners { get; set; } = [];

    public IReadOnlyList<string> ExcludedRepos { get; set; } = [];

    public bool PollIssueEdits { get; set; } = true;

    public bool PollEvents { get; set; } = true;

    public string MentionedUser { get; set; } = string.Empty;

    public int MaxIssues { get; set; } = 10;

    public GitHubPullRequestFilterOptions PullRequests { get; set; } = new();
}
