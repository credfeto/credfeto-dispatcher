using System;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class EventRefreshQueueTests : TestBase
{
    private const string REPO = "owner/repo";

    private static readonly DateTimeOffset Earlier = new(
        year: 2024,
        month: 1,
        day: 1,
        hour: 0,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    private static readonly DateTimeOffset Later = Earlier.AddMinutes(5);

    private readonly EventRefreshQueue _queue = new([REPO]);

    [Fact]
    public void AddPullRequestQueuesEachPullRequestOnce()
    {
        Assert.True(
            this._queue.AddPullRequest(repo: REPO, number: 1, pendingSince: Earlier),
            userMessage: "First add queues"
        );
        Assert.False(
            this._queue.AddPullRequest(repo: "Owner/Repo", number: 1, pendingSince: Later),
            userMessage: "Second add is a duplicate"
        );

        Assert.Equal(expected: (REPO, 1, Earlier), actual: Assert.Single(this._queue.PullRequests));
    }

    [Fact]
    public void AddPullRequestIgnoresUntrackedReposAndInvalidNumbers()
    {
        Assert.False(
            this._queue.AddPullRequest(repo: "owner/other", number: 1, pendingSince: Earlier),
            userMessage: "Untracked repo"
        );
        Assert.False(
            this._queue.AddPullRequest(repo: REPO, number: 0, pendingSince: Earlier),
            userMessage: "No number"
        );

        Assert.Empty(this._queue.PullRequests);
    }

    [Fact]
    public void AddBranchQueuesEachBranchOnce()
    {
        Assert.True(
            this._queue.AddBranch(repo: REPO, branch: "feature/x", pendingSince: Earlier),
            userMessage: "First add queues"
        );
        Assert.False(
            this._queue.AddBranch(repo: REPO, branch: "feature/x", pendingSince: Later),
            userMessage: "Duplicate"
        );
        Assert.False(
            this._queue.AddBranch(repo: REPO, branch: string.Empty, pendingSince: Earlier),
            userMessage: "Empty branch"
        );

        Assert.Equal(expected: (REPO, "feature/x", Earlier), actual: Assert.Single(this._queue.Branches));
    }

    [Fact]
    public void DuplicatesKeepTheOldestPendingTimeAndTheNewestCommentTime()
    {
        this._queue.AddIssue(repo: REPO, number: 7, commentedAt: Later);
        this._queue.AddIssue(repo: REPO, number: 7, commentedAt: Earlier);
        this._queue.AddPullRequest(repo: REPO, number: 8, pendingSince: Later);
        this._queue.AddPullRequest(repo: REPO, number: 8, pendingSince: Earlier);

        Assert.Equal(expected: (REPO, 7, Later, Earlier), actual: Assert.Single(this._queue.Issues));
        Assert.Equal(expected: (REPO, 8, Earlier), actual: Assert.Single(this._queue.PullRequests));
    }

    [Fact]
    public void OldestPendingSinceIsNullWhenNothingIsQueued()
    {
        Assert.Null(this._queue.OldestPendingSince);
        Assert.Equal(expected: 0, actual: this._queue.Count);
    }

    [Fact]
    public void OldestPendingSinceIsTheOldestEventAcrossEveryKindOfItem()
    {
        this._queue.AddPullRequest(repo: REPO, number: 1, pendingSince: Later);
        this._queue.AddBranch(repo: REPO, branch: "feature/x", pendingSince: Later);
        this._queue.AddIssue(repo: REPO, number: 2, commentedAt: Earlier);

        Assert.Equal(expected: Earlier, actual: this._queue.OldestPendingSince);
    }

    [Fact]
    public void CarriedOverItemsComeFirstAndKeepTheirPendingTime()
    {
        EventRefreshQueue previous = new([REPO, "owner/dropped"]);
        previous.AddIssue(repo: REPO, number: 3, commentedAt: Earlier);
        previous.AddIssue(repo: "owner/dropped", number: 4, commentedAt: Earlier);
        previous.AddPullRequest(repo: REPO, number: 5, pendingSince: Earlier);

        EventRefreshQueue queue = new(trackedRepos: [REPO], carriedOver: previous);
        queue.AddIssue(repo: REPO, number: 6, commentedAt: Later);
        queue.AddIssue(repo: REPO, number: 3, commentedAt: Later);

        Assert.Equal(expected: 1, actual: queue.CarriedIssueCount);
        Assert.Equal(expected: 1, actual: queue.CarriedPullRequestCount);
        Assert.Equal(expected: 0, actual: queue.CarriedBranchCount);
        Assert.Equal(expected: [(REPO, 3, Later, Earlier), (REPO, 6, Later, Later)], actual: queue.Issues);
    }

    [Fact]
    public void AddDependencyBranchChangeKeepsOnlyDependencyBranchesInTrackedRepos()
    {
        DateTimeOffset at = DateTimeOffset.UnixEpoch;

        Assert.True(
            this._queue.AddDependencyBranchChange(repo: "owner/repo", branch: "depends/x", isDeleted: false, at: at),
            userMessage: "Dependency branch"
        );
        Assert.True(
            this._queue.AddDependencyBranchChange(repo: "owner/repo", branch: "depends/x", isDeleted: true, at: at),
            userMessage: "Its deletion"
        );
        Assert.False(
            this._queue.AddDependencyBranchChange(repo: "owner/repo", branch: "feature/x", isDeleted: false, at: at),
            userMessage: "Not a dependency branch"
        );
        Assert.False(
            this._queue.AddDependencyBranchChange(repo: "owner/other", branch: "depends/x", isDeleted: false, at: at),
            userMessage: "Untracked repo"
        );

        Assert.Equal(
            expected:
            [
                new DependencyBranchChange(Repository: "owner/repo", Branch: "depends/x", IsDeleted: false, At: at),
                new DependencyBranchChange(Repository: "owner/repo", Branch: "depends/x", IsDeleted: true, At: at),
            ],
            actual: this._queue.DependencyBranchChanges
        );
    }
}
