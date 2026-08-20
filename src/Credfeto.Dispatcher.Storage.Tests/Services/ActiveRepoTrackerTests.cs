using System.Collections.Generic;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;
using FunFair.Test.Common;
using FunFair.Test.Common.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class ActiveRepoTrackerTests : TestBase
{
    private static readonly string[] TwoRepos = ["owner/repo-a", "owner/repo-b"];
    private static readonly string[] NoRepos = [];
    private static readonly string[] DuplicateRepos = ["owner/repo-a", "Owner/Repo-A", "owner/repo-a"];

    private readonly TestDatabaseStub _database;
    private readonly IActiveRepoTracker _tracker;

    public ActiveRepoTrackerTests()
    {
        this._database = new TestDatabaseStub();
        this._tracker = new ActiveRepoTracker(this._database);
    }

    public static TheoryData<IReadOnlyList<string>> ActiveReposCases() => [TwoRepos, NoRepos, DuplicateRepos];

    [Theory]
    [MemberData(nameof(ActiveReposCases))]
    public async Task UpdateActiveReposAsync_CallsDatabaseOnceAsync(IReadOnlyList<string> activeRepos)
    {
        await this._tracker.UpdateActiveReposAsync(
            activeRepos: activeRepos,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 1, actual: this._database.VoidExecuteCallCount);
    }
}
