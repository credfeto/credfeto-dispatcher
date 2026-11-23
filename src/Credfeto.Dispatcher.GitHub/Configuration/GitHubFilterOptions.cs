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

    public IReadOnlyList<string> BoostedRepos { get; set; } = [];

    public bool PollIssueEdits { get; set; } = true;

    public bool PollEvents { get; set; } = true;

    public string MentionedUser { get; set; } = string.Empty;

    // Logins (matched case-insensitively) whose comments on a plain issue never trigger a re-read of that issue, on top
    // of bot accounts: e.g. an automation that posts as a normal user account. Empty by default.
    public IReadOnlyList<string> IgnoredCommenters { get; set; } = [];

    // Maximum number of regular (below Urgent) issues returned per owner by /priorities.
    // Urgent and Security issues are never capped. A value of 0 or less means no cap.
    public int MaxIssues { get; set; } = 10;

    // Upper bound on the GitHub calls one event-poll cycle spends re-reading the items its events touched (PR events,
    // reviews, comments, pushes, new branches, and human comments on plain issues): each open-PR lookup for a pushed or
    // new dependency branch and each PR or issue re-read counts one. PRs past the bound are handed to the CI refresh instead; issues wait for the next
    // scan. A value of 0 or less hands them all over.
    public int MaxEventRefreshesPerPoll { get; set; } = 20;

    // How many open PRs with unknown, pending or failed checks one poll cycle re-checks. Each costs one conditional
    // request, plus a full re-read only when its workflow runs changed. A value of 0 or less turns the CI refresh off.
    public int MaxCheckRefreshesPerPoll { get; set; } = 5;

    public GitHubPullRequestFilterOptions PullRequests { get; set; } = new();
}
