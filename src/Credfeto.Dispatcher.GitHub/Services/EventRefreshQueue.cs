using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Services;

// The single items one event-poll cycle needs to re-read, de-duplicated across every feed read in the cycle (the
// repo and owner feeds overlap) and limited to the repos the dispatcher tracks. Each item keeps the time of the oldest
// event that queued it (PendingSince), so a cycle that cannot re-read everything knows how far the store is current.
// Items carried over from the previous cycle are queued first, ahead of the new ones.
[DebuggerDisplay(
    "PullRequests: {PullRequests.Count}, Issues: {Issues.Count}, Branches: {Branches.Count}, Carried: {CarriedBranchCount}/{CarriedPullRequestCount}/{CarriedIssueCount}, Dependency branch changes: {DependencyBranchChanges.Count}"
)]
internal sealed class EventRefreshQueue
{
    private readonly List<(string Repo, string Branch, DateTimeOffset PendingSince)> _branches = [];
    private readonly List<DependencyBranchChange> _dependencyBranchChanges = [];
    private readonly List<(string Repo, int Number, DateTimeOffset CommentedAt, DateTimeOffset PendingSince)> _issues =
    [];
    private readonly HashSet<string> _keys = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly List<(string Repo, int Number, DateTimeOffset PendingSince)> _pullRequests = [];
    private readonly HashSet<string> _trackedRepos;

    public EventRefreshQueue(IEnumerable<string> trackedRepos, EventRefreshQueue? carriedOver = null)
    {
        this._trackedRepos = [with(StringComparer.OrdinalIgnoreCase), .. trackedRepos];

        if (carriedOver is not null)
        {
            this.AddAll(carriedOver);
        }

        this.CarriedBranchCount = this._branches.Count;
        this.CarriedPullRequestCount = this._pullRequests.Count;
        this.CarriedIssueCount = this._issues.Count;
    }

    public IReadOnlyList<(string Repo, int Number, DateTimeOffset PendingSince)> PullRequests => this._pullRequests;

    public IReadOnlyList<(string Repo, string Branch, DateTimeOffset PendingSince)> Branches => this._branches;

    public IReadOnlyList<(string Repo, int Number, DateTimeOffset CommentedAt, DateTimeOffset PendingSince)> Issues =>
        this._issues;

    // How many of the items at the front of each list came from the previous cycle.
    public int CarriedBranchCount { get; }

    public int CarriedPullRequestCount { get; }

    public int CarriedIssueCount { get; }

    public int Count => this._branches.Count + this._pullRequests.Count + this._issues.Count;

    // The time of the oldest event still waiting for a re-read, or null when nothing is queued.
    public DateTimeOffset? OldestPendingSince =>
        this
            ._branches.Select(static b => (DateTimeOffset?)b.PendingSince)
            .Concat(this._pullRequests.Select(static p => (DateTimeOffset?)p.PendingSince))
            .Concat(this._issues.Select(static i => (DateTimeOffset?)i.PendingSince))
            .Min();

    // Dependency branches created or deleted, in the order the events were processed (oldest first per feed).
    public IReadOnlyList<DependencyBranchChange> DependencyBranchChanges => this._dependencyBranchChanges;

    public bool AddDependencyBranchChange(string repo, string branch, bool isDeleted, in DateTimeOffset at)
    {
        if (!DependencyBranchNames.IsDependencyBranch(branch) || !this._trackedRepos.Contains(repo))
        {
            return false;
        }

        this._dependencyBranchChanges.Add(
            new DependencyBranchChange(Repository: repo, Branch: branch, IsDeleted: isDeleted, At: at)
        );

        return true;
    }

    // Several comments on one issue in a cycle cost one re-read, stamped with the newest comment.
    public bool AddIssue(string repo, int number, in DateTimeOffset commentedAt)
    {
        return this.AddIssue(repo: repo, number: number, commentedAt: commentedAt, pendingSince: commentedAt);
    }

    public bool AddIssue(string repo, int number, in DateTimeOffset commentedAt, in DateTimeOffset pendingSince)
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

            if (index >= 0)
            {
                (_, _, DateTimeOffset existingCommentedAt, DateTimeOffset existingPendingSince) = this._issues[index];
                this._issues[index] = (
                    repo,
                    number,
                    Max(existingCommentedAt, commentedAt),
                    Min(existingPendingSince, pendingSince)
                );
            }

            return false;
        }

        this._issues.Add((repo, number, commentedAt, pendingSince));

        return true;
    }

    public bool AddPullRequest(string repo, int number, in DateTimeOffset pendingSince)
    {
        if (number <= 0 || !this._trackedRepos.Contains(repo))
        {
            return false;
        }

        if (!this._keys.Add(string.Concat("pr:", repo, "#", number.ToString(CultureInfo.InvariantCulture))))
        {
            int index = this._pullRequests.FindIndex(p =>
                p.Number == number && StringComparer.OrdinalIgnoreCase.Equals(p.Repo, repo)
            );

            if (index >= 0 && pendingSince < this._pullRequests[index].PendingSince)
            {
                this._pullRequests[index] = (repo, number, pendingSince);
            }

            return false;
        }

        this._pullRequests.Add((repo, number, pendingSince));

        return true;
    }

    public bool AddBranch(string repo, string branch, in DateTimeOffset pendingSince)
    {
        if (string.IsNullOrEmpty(branch) || !this._trackedRepos.Contains(repo))
        {
            return false;
        }

        if (!this._keys.Add(string.Concat("branch:", repo, ":", branch)))
        {
            int index = this._branches.FindIndex(b =>
                StringComparer.Ordinal.Equals(b.Branch, branch) && StringComparer.OrdinalIgnoreCase.Equals(b.Repo, repo)
            );

            if (index >= 0 && pendingSince < this._branches[index].PendingSince)
            {
                this._branches[index] = (repo, branch, pendingSince);
            }

            return false;
        }

        this._branches.Add((repo, branch, pendingSince));

        return true;
    }

    private void AddAll(EventRefreshQueue other)
    {
        foreach ((string repo, string branch, DateTimeOffset pendingSince) in other.Branches)
        {
            this.AddBranch(repo: repo, branch: branch, pendingSince: pendingSince);
        }

        foreach ((string repo, int number, DateTimeOffset pendingSince) in other.PullRequests)
        {
            this.AddPullRequest(repo: repo, number: number, pendingSince: pendingSince);
        }

        foreach ((string repo, int number, DateTimeOffset commentedAt, DateTimeOffset pendingSince) in other.Issues)
        {
            this.AddIssue(repo: repo, number: number, commentedAt: commentedAt, pendingSince: pendingSince);
        }
    }

    private static DateTimeOffset Max(in DateTimeOffset left, in DateTimeOffset right)
    {
        return left > right ? left : right;
    }

    private static DateTimeOffset Min(in DateTimeOffset left, in DateTimeOffset right)
    {
        return left < right ? left : right;
    }
}
