using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace Credfeto.Dispatcher.GitHub.Services;

// The single items one event-poll cycle needs to re-read, de-duplicated across every feed read in the cycle (the
// repo and owner feeds overlap) and limited to the repos the dispatcher tracks.
[DebuggerDisplay("PullRequests: {PullRequests.Count}, Issues: {Issues.Count}, Branches: {Branches.Count}")]
internal sealed class EventRefreshQueue
{
    private readonly List<(string Repo, string Branch)> _branches = [];
    private readonly List<(string Repo, int Number, DateTimeOffset CommentedAt)> _issues = [];
    private readonly HashSet<string> _keys = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly List<(string Repo, int Number)> _pullRequests = [];
    private readonly HashSet<string> _trackedRepos;

    public EventRefreshQueue(IEnumerable<string> trackedRepos)
    {
        this._trackedRepos = [with(StringComparer.OrdinalIgnoreCase), .. trackedRepos];
    }

    public IReadOnlyList<(string Repo, int Number)> PullRequests => this._pullRequests;

    public IReadOnlyList<(string Repo, string Branch)> Branches => this._branches;

    public IReadOnlyList<(string Repo, int Number, DateTimeOffset CommentedAt)> Issues => this._issues;

    // Several comments on one issue in a cycle cost one re-read, stamped with the newest comment.
    public bool AddIssue(string repo, int number, in DateTimeOffset commentedAt)
    {
        if (number <= 0 || !this._trackedRepos.Contains(repo))
        {
            return false;
        }

        if (!this._keys.Add(string.Concat("issue:", repo, "#", number.ToString(CultureInfo.InvariantCulture))))
        {
            int index = this._issues.FindIndex(i =>
                i.Number == number && StringComparer.OrdinalIgnoreCase.Equals(i.Repo, repo)
            );

            if (index >= 0 && commentedAt > this._issues[index].CommentedAt)
            {
                this._issues[index] = (repo, number, commentedAt);
            }

            return false;
        }

        this._issues.Add((repo, number, commentedAt));

        return true;
    }

    public bool AddPullRequest(string repo, int number)
    {
        if (number <= 0 || !this._trackedRepos.Contains(repo))
        {
            return false;
        }

        if (!this._keys.Add(string.Concat("pr:", repo, "#", number.ToString(CultureInfo.InvariantCulture))))
        {
            return false;
        }

        this._pullRequests.Add((repo, number));

        return true;
    }

    public bool AddBranch(string repo, string branch)
    {
        if (string.IsNullOrEmpty(branch) || !this._trackedRepos.Contains(repo))
        {
            return false;
        }

        if (!this._keys.Add(string.Concat("branch:", repo, ":", branch)))
        {
            return false;
        }

        this._branches.Add((repo, branch));

        return true;
    }
}
