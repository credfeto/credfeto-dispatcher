using System;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class EventRefreshQueueTests : TestBase
{
    private readonly EventRefreshQueue _queue = new(["owner/repo"]);

    [Fact]
    public void AddPullRequestQueuesEachPullRequestOnce()
    {
        Assert.True(this._queue.AddPullRequest(repo: "owner/repo", number: 1), userMessage: "First add queues");
        Assert.False(
            this._queue.AddPullRequest(repo: "Owner/Repo", number: 1),
            userMessage: "Second add is a duplicate"
        );

        Assert.Equal(expected: ("owner/repo", 1), actual: Assert.Single(this._queue.PullRequests));
    }

    [Fact]
    public void AddPullRequestIgnoresUntrackedReposAndInvalidNumbers()
    {
        Assert.False(this._queue.AddPullRequest(repo: "owner/other", number: 1), userMessage: "Untracked repo");
        Assert.False(this._queue.AddPullRequest(repo: "owner/repo", number: 0), userMessage: "No number");

        Assert.Empty(this._queue.PullRequests);
    }

    [Fact]
    public void AddBranchQueuesEachBranchOnce()
    {
        Assert.True(this._queue.AddBranch(repo: "owner/repo", branch: "feature/x"), userMessage: "First add queues");
        Assert.False(this._queue.AddBranch(repo: "owner/repo", branch: "feature/x"), userMessage: "Duplicate");
        Assert.False(this._queue.AddBranch(repo: "owner/repo", branch: string.Empty), userMessage: "Empty branch");

        Assert.Equal(expected: ("owner/repo", "feature/x"), actual: Assert.Single(this._queue.Branches));
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
