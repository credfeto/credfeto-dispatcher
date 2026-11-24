using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database.Rows;
using Credfeto.Dispatcher.Storage.Services;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class DependencyBranchStoreTests : TestBase
{
    private const string REPOSITORY = "owner/repo";
    private const string BRANCH = "depends/update-sdk";

    private static readonly DateTimeOffset BaseTime = MockDateTimeSources.Past.GetUtcNow();

    private readonly TestDatabaseStub _database;
    private readonly IDependencyBranchStore _store;

    public DependencyBranchStoreTests()
    {
        this._database = new TestDatabaseStub();
        this._store = new DependencyBranchStore(this._database);
    }

    [Fact]
    public async Task EachWriteCallsTheDatabaseOnceAsync()
    {
        await this._store.RecordCreatedAsync(
            repository: REPOSITORY,
            branch: BRANCH,
            createdAt: BaseTime,
            cancellationToken: this.CancellationToken()
        );
        await this._store.RecordDeletedAsync(
            repository: REPOSITORY,
            branch: BRANCH,
            deletedAt: BaseTime,
            cancellationToken: this.CancellationToken()
        );
        await this._store.RecordPullRequestAsync(
            repository: REPOSITORY,
            branch: BRANCH,
            pullRequestNumber: 7,
            cancellationToken: this.CancellationToken()
        );
        await this._store.RecordCheckedAsync(
            repository: REPOSITORY,
            branch: BRANCH,
            eTag: null,
            cancellationToken: this.CancellationToken()
        );
        await this._store.PruneAsync(
            settledBefore: BaseTime,
            firstSeenBefore: BaseTime,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 5, actual: this._database.VoidExecuteCallCount);
    }

    [Fact]
    public async Task GetDueForRecheckMapsTheRowsAsync()
    {
        IReadOnlyList<DependencyBranchRow> rows =
        [
            new(
                Repository: REPOSITORY,
                Branch: BRANCH,
                FirstSeen: BaseTime,
                LastChecked: null,
                LastUpdated: BaseTime,
                PullRequestNumber: null,
                DeletedAt: null,
                ETag: "\"e\""
            ),
        ];
        this._database.SetReturn(rows);

        IReadOnlyList<DependencyBranchRecheck> due = await this._store.GetDueForRecheckAsync(
            maxCount: 3,
            checkedBefore: BaseTime,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(
            expected: new DependencyBranchRecheck(Repository: REPOSITORY, Branch: BRANCH, ETag: "\"e\""),
            actual: Assert.Single(due)
        );
    }

    [Fact]
    public async Task GetDueForRecheckWithNoBudgetReturnsNothingAsync()
    {
        IReadOnlyList<DependencyBranchRecheck> due = await this._store.GetDueForRecheckAsync(
            maxCount: 0,
            checkedBefore: BaseTime,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(due);
    }
}
