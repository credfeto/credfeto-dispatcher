using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;
using FunFair.Test.Common;
using FunFair.Test.Common.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class ActiveRepoTrackerTests : TestBase
{
    private readonly TestDatabaseStub _database;
    private readonly IActiveRepoTracker _tracker;

    public ActiveRepoTrackerTests()
    {
        this._database = new TestDatabaseStub();
        this._tracker = new ActiveRepoTracker(this._database);
    }

    [Fact]
    public async Task UpdateActiveReposAsync_CallsDatabaseAsync()
    {
        await this._tracker.UpdateActiveReposAsync(
            activeRepos: ["owner/repo-a", "owner/repo-b"],
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 1, actual: this._database.VoidExecuteCallCount);
    }

    [Fact]
    public async Task UpdateActiveReposAsync_WithEmptyList_CallsDatabaseAsync()
    {
        await this._tracker.UpdateActiveReposAsync(activeRepos: [], cancellationToken: this.CancellationToken());

        Assert.Equal(expected: 1, actual: this._database.VoidExecuteCallCount);
    }
}
