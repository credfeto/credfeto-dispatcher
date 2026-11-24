using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.Storage.Database.Rows;
using Credfeto.Dispatcher.Storage.InMemory;
using Credfeto.Dispatcher.Storage.Services;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.InMemory;

public sealed class InMemoryDependencyBranchStoreTests : TestBase
{
    private const string REPOSITORY = "owner/repo";
    private const string OTHER_REPOSITORY = "other/thing";
    private const string BRANCH = "depends/update-sdk";
    private const string OTHER_BRANCH = "dependabot/npm/left-pad-2.0.0";

    private static readonly TimeSpan OrphanAge = TimeSpan.FromHours(6);

    private readonly DateTimeOffset _now;
    private readonly InMemoryWorkItemRepository _repository;
    private readonly InMemoryDispatcherStore _store;
    private readonly InMemoryDependencyBranchStore _branches;

    public InMemoryDependencyBranchStoreTests()
    {
        TimeProvider timeProvider = MockDateTimeSources.Past;
        this._now = timeProvider.GetUtcNow();
        this._store = new InMemoryDispatcherStore(timeProvider);
        this._branches = new InMemoryDependencyBranchStore(this._store);
        this._repository = new InMemoryWorkItemRepository(
            store: this._store,
            feedFreshnessStore: new FeedFreshnessStore(new InMemoryETagStore(this._store)),
            timeProvider: timeProvider
        );
    }

    private ValueTask CreateAsync(string branch, in TimeSpan age, string repository = REPOSITORY)
    {
        return this._branches.RecordCreatedAsync(
            repository: repository,
            branch: branch,
            createdAt: this._now - age,
            cancellationToken: this.CancellationToken()
        );
    }

    private ValueTask DeleteAsync(string branch, in TimeSpan age)
    {
        return this._branches.RecordDeletedAsync(
            repository: REPOSITORY,
            branch: branch,
            deletedAt: this._now - age,
            cancellationToken: this.CancellationToken()
        );
    }

    private async Task<IReadOnlyList<DependencyBranchState>> OrphansAsync(string? owner = null, string? repo = null)
    {
        DbStateResponse response = await this._repository.GetDatabaseStateAsync(
            owner: owner,
            repo: repo,
            user: null,
            orphanedBranchAge: OrphanAge,
            cancellationToken: this.CancellationToken()
        );

        return response.OrphanedDependencyBranches;
    }

    private async Task<IReadOnlyList<string>> DueBranchesAsync(int maxCount = 3)
    {
        IReadOnlyList<DependencyBranchRecheck> due = await this._branches.GetDueForRecheckAsync(
            maxCount: maxCount,
            checkedBefore: this._now - TimeSpan.FromMinutes(15),
            cancellationToken: this.CancellationToken()
        );

        return [.. due.Select(static d => d.Branch)];
    }

    [Fact]
    public async Task ABranchWithNoPullRequestIsOrphanedOnlyOnceOlderThanTheThresholdAsync()
    {
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(7));
        await this.CreateAsync(branch: OTHER_BRANCH, age: TimeSpan.FromHours(5));

        DependencyBranchState orphan = Assert.Single(await this.OrphansAsync());
        Assert.Equal(expected: BRANCH, actual: orphan.Branch);
        Assert.Equal(expected: REPOSITORY, actual: orphan.Repository);
        Assert.Equal(expected: "owner", actual: orphan.Owner);
        Assert.Equal(expected: "repo", actual: orphan.Repo);
        Assert.Equal(expected: this._now - TimeSpan.FromHours(7), actual: orphan.FirstSeen);
        Assert.Null(orphan.LastChecked);
    }

    [Fact]
    public async Task OrphansFollowTheOwnerAndRepoFiltersAsync()
    {
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(7));
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(7), repository: OTHER_REPOSITORY);

        Assert.Equal(expected: 2, actual: (await this.OrphansAsync()).Count);
        Assert.Equal(
            expected: OTHER_REPOSITORY,
            actual: Assert.Single(await this.OrphansAsync(owner: "other")).Repository
        );
        Assert.Equal(expected: REPOSITORY, actual: Assert.Single(await this.OrphansAsync(repo: "repo")).Repository);
    }

    [Fact]
    public async Task APullRequestForTheBranchResolvesItAsync()
    {
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(7));

        await this._branches.RecordPullRequestAsync(
            repository: REPOSITORY,
            branch: BRANCH,
            pullRequestNumber: 12,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(await this.OrphansAsync());
        Assert.Empty(await this.DueBranchesAsync());
    }

    [Fact]
    public async Task APullRequestForAnUntrackedBranchAddsNothingAsync()
    {
        await this._branches.RecordPullRequestAsync(
            repository: REPOSITORY,
            branch: BRANCH,
            pullRequestNumber: 12,
            cancellationToken: this.CancellationToken()
        );
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(7));

        Assert.Single(await this.OrphansAsync());
    }

    [Fact]
    public async Task DeletingTheBranchRemovesItFromTheOrphansAsync()
    {
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(7));
        await this.DeleteAsync(branch: BRANCH, age: TimeSpan.FromHours(1));

        Assert.Empty(await this.OrphansAsync());
        Assert.Empty(await this.DueBranchesAsync());
    }

    [Fact]
    public async Task ADeletionBeforeTheBranchWasCreatedIsIgnoredAsync()
    {
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(7));
        await this.DeleteAsync(branch: BRANCH, age: TimeSpan.FromHours(8));

        Assert.Single(await this.OrphansAsync());
    }

    [Fact]
    public async Task ABranchCreatedAgainAfterItsDeletionIsTrackedAfreshAsync()
    {
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(20));
        await this.DeleteAsync(branch: BRANCH, age: TimeSpan.FromHours(10));
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(8));

        Assert.Equal(
            expected: this._now - TimeSpan.FromHours(8),
            actual: Assert.Single(await this.OrphansAsync()).FirstSeen
        );
    }

    [Fact]
    public async Task ARepeatedCreateKeepsTheFirstSightingAsync()
    {
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(7));
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(1));

        Assert.Equal(
            expected: this._now - TimeSpan.FromHours(7),
            actual: Assert.Single(await this.OrphansAsync()).FirstSeen
        );
    }

    [Fact]
    public async Task RecheckPicksTheLeastRecentlyLookedAtBranchesWithinTheBoundAsync()
    {
        await this.CreateAsync(branch: "depends/a", age: TimeSpan.FromHours(3));
        await this.CreateAsync(branch: "depends/b", age: TimeSpan.FromHours(2));
        await this.CreateAsync(branch: "depends/c", age: TimeSpan.FromHours(1));
        await this.CreateAsync(branch: "depends/d", age: TimeSpan.FromHours(4));
        await this.CreateAsync(branch: "depends/new", age: TimeSpan.FromMinutes(5));

        Assert.Equal(expected: ["depends/d", "depends/a", "depends/b"], actual: await this.DueBranchesAsync());
        Assert.Equal(expected: ["depends/d"], actual: await this.DueBranchesAsync(maxCount: 1));
        Assert.Empty(await this.DueBranchesAsync(maxCount: 0));
    }

    [Fact]
    public async Task ABranchJustCheckedIsNotDueAgainAsync()
    {
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(3));
        await this.CreateAsync(branch: OTHER_BRANCH, age: TimeSpan.FromHours(2));

        await this._branches.RecordCheckedAsync(
            repository: REPOSITORY,
            branch: BRANCH,
            eTag: "\"e1\"",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: [OTHER_BRANCH], actual: await this.DueBranchesAsync());
    }

    [Fact]
    public async Task RecordCheckedWithoutAnETagKeepsTheStoredOneAsync()
    {
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(7));
        await this._branches.RecordCheckedAsync(
            repository: REPOSITORY,
            branch: BRANCH,
            eTag: "\"e1\"",
            cancellationToken: this.CancellationToken()
        );
        await this._branches.RecordCheckedAsync(
            repository: REPOSITORY,
            branch: BRANCH,
            eTag: null,
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<DependencyBranchRecheck> due = await this._branches.GetDueForRecheckAsync(
            maxCount: 3,
            checkedBefore: this._now + TimeSpan.FromMinutes(1),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: "\"e1\"", actual: Assert.Single(due).ETag);
        Assert.Equal(expected: this._now, actual: Assert.Single(await this.OrphansAsync()).LastChecked);
    }

    [Fact]
    public async Task PruneRemovesSettledAndVeryOldBranchesOnlyAsync()
    {
        await this.CreateAsync(branch: "depends/resolved", age: TimeSpan.FromHours(7));
        await this._branches.RecordPullRequestAsync(
            repository: REPOSITORY,
            branch: "depends/resolved",
            pullRequestNumber: 3,
            cancellationToken: this.CancellationToken()
        );
        await this.CreateAsync(branch: "depends/deleted", age: TimeSpan.FromHours(7));
        await this.DeleteAsync(branch: "depends/deleted", age: TimeSpan.FromHours(6));
        await this.CreateAsync(branch: "depends/ancient", age: TimeSpan.FromDays(40));
        await this.CreateAsync(branch: "depends/open", age: TimeSpan.FromHours(7));

        await this._branches.PruneAsync(
            settledBefore: this._now + TimeSpan.FromMinutes(1),
            firstSeenBefore: this._now - TimeSpan.FromDays(30),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: ["depends/open"], actual: [.. (await this.OrphansAsync()).Select(static o => o.Branch)]);

        await this.CreateAsync(branch: "depends/resolved", age: TimeSpan.FromHours(7));
        Assert.Equal(expected: 2, actual: (await this.OrphansAsync()).Count);
    }

    [Fact]
    public async Task PruneKeepsRecentlySettledBranchesAsync()
    {
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(7));
        await this.DeleteAsync(branch: BRANCH, age: TimeSpan.FromHours(1));

        await this._branches.PruneAsync(
            settledBefore: this._now - TimeSpan.FromDays(7),
            firstSeenBefore: this._now - TimeSpan.FromDays(30),
            cancellationToken: this.CancellationToken()
        );

        // Still tracked as deleted, so an older CreateEvent seen again does not resurrect it.
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(7));
        Assert.Empty(await this.OrphansAsync());
    }

    [Fact]
    public async Task SnapshotRoundTripKeepsTrackedBranchesAsync()
    {
        await this.CreateAsync(branch: BRANCH, age: TimeSpan.FromHours(7));

        InMemoryDispatcherStore restored = new(MockDateTimeSources.Past);
        restored.ImportSnapshot(this._store.ExportSnapshot());

        DependencyBranchRow row = Assert.Single(
            restored.GetOrphanedDependencyBranches(owner: null, repo: null, firstSeenBefore: this._now)
        );
        Assert.Equal(expected: BRANCH, actual: row.Branch);
    }
}
