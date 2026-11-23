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
}
