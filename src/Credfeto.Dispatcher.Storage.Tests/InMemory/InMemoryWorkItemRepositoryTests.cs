using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.Storage.InMemory;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.InMemory;

public sealed class InMemoryWorkItemRepositoryTests : TestBase
{
    private const string REPOSITORY = "owner/repo";
    private const string OTHER_REPOSITORY = "other/repo";

    private readonly InMemoryWorkItemRepository _repository;
    private readonly InMemoryDispatcherStore _store;

    public InMemoryWorkItemRepositoryTests()
    {
        TimeProvider timeProvider = MockDateTimeSources.Past;
        this._store = new InMemoryDispatcherStore(timeProvider);
        this._repository = new InMemoryWorkItemRepository(store: this._store, timeProvider: timeProvider);
    }

    [Fact]
    public async Task GetDatabaseStateAsyncReturnsStoredItemsAsync()
    {
        this.UpsertOpenPullRequest(repository: REPOSITORY, id: 10);
        this.UpsertOpenIssue(repository: REPOSITORY, id: 20);

        DbStateResponse response = await this._repository.GetDatabaseStateAsync(
            owner: null,
            repo: null,
            user: null,
            cancellationToken: this.CancellationToken()
        );

        PullRequestState pullRequest = Assert.Single(response.PullRequests);
        Assert.Equal(expected: 10, actual: pullRequest.Number);
        Assert.Equal(expected: REPOSITORY, actual: pullRequest.Repository);

        IssueState issue = Assert.Single(response.Issues);
        Assert.Equal(expected: 20, actual: issue.Number);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncReturnsActiveItemsAsync()
    {
        this.UpsertOpenPullRequest(repository: REPOSITORY, id: 10);

        PrioritiesResponse response = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: ["owner"],
            maxIssues: 10,
            boostedRepos: [],
            user: null,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotEmpty(response.Priorities);
    }

    [Fact]
    public async Task WithNoConfigOwnersAreOrderedAlphabeticallyAsync()
    {
        this.UpsertPr("zz-owner/repo", 1);
        this.UpsertPr("aa-owner/repo", 2);

        Assert.Equal(expected: ["aa-owner/repo", "zz-owner/repo"], actual: await this.GetRepositoriesAsync(owners: []));
    }

    [Fact]
    public async Task WithOwnersConfiguredOwnersAreOrderedByConfigAsync()
    {
        this.UpsertPr("aa-owner/repo", 1);
        this.UpsertPr("zz-owner/repo", 2);

        Assert.Equal(
            expected: ["zz-owner/repo", "aa-owner/repo"],
            actual: await this.GetRepositoriesAsync(owners: ["zz-owner", "aa-owner"])
        );
    }

    [Fact]
    public async Task PullRequestsAppearBeforeIssuesAsync()
    {
        this.UpsertIssue("aa/repo", 1, WorkPriority.MEDIUM);
        this.UpsertPr("zz/repo", 2);

        Assert.Equal(expected: ["zz/repo", "aa/repo"], actual: await this.GetRepositoriesAsync(owners: []));
    }

    [Fact]
    public async Task OnlyTheHighestPriorityIssuePerRepoIsReturnedAsync()
    {
        this.UpsertIssue("owner/repo", 1, WorkPriority.LOW);
        this.UpsertIssue("owner/repo", 2, WorkPriority.HIGH);
        this.UpsertIssue("owner/repo", 3, WorkPriority.MEDIUM);

        PrioritiesResponse response = await this.GetAsync(owners: []);

        Assert.Equal(expected: 2, actual: Assert.Single(response.Priorities).Id);
    }

    [Fact]
    public async Task EachRepoContributesAtMostOneIssueAsync()
    {
        this.UpsertIssue("owner/a", 1, WorkPriority.MEDIUM);
        this.UpsertIssue("owner/a", 2, WorkPriority.MEDIUM);
        this.UpsertIssue("owner/b", 3, WorkPriority.MEDIUM);

        PrioritiesResponse response = await this.GetAsync(owners: []);

        Assert.Equal(expected: 2, actual: response.Priorities.Count);
    }

    [Fact]
    public async Task MaxIssuesCapAppliesPerOwnerAndKeepsPullRequestsAsync()
    {
        this.UpsertPr("a/pr1", 1);
        this.UpsertPr("a/pr2", 2);
        this.UpsertIssue("a/r1", 3, WorkPriority.MEDIUM);
        this.UpsertIssue("a/r2", 4, WorkPriority.MEDIUM);
        this.UpsertIssue("b/r1", 5, WorkPriority.MEDIUM);
        this.UpsertIssue("b/r2", 6, WorkPriority.MEDIUM);

        PrioritiesResponse response = await this.GetAsync(owners: [], maxIssues: 1);

        Assert.Equal(
            expected: 2,
            actual: response.Priorities.Count(static p =>
                string.Equals(p.ItemType, "PullRequest", StringComparison.Ordinal)
            )
        );
        Assert.Equal(
            expected: 2,
            actual: response.Priorities.Count(static p => string.Equals(p.ItemType, "Issue", StringComparison.Ordinal))
        );
    }

    [Fact]
    public async Task MaxIssuesCapGroupsOwnersCaseInsensitivelyAsync()
    {
        this.UpsertIssue("Owner/r1", 1, WorkPriority.MEDIUM);
        this.UpsertIssue("owner/r2", 2, WorkPriority.MEDIUM);

        PrioritiesResponse response = await this.GetAsync(owners: [], maxIssues: 1);

        Assert.Single(response.Priorities);
    }

    [Fact]
    public async Task UrgentIssuesBypassTheCapAsync()
    {
        this.UpsertIssue("a/r1", 1, WorkPriority.URGENT);
        this.UpsertIssue("a/r2", 2, WorkPriority.URGENT);
        this.UpsertIssue("a/r3", 3, WorkPriority.MEDIUM);
        this.UpsertIssue("a/r4", 4, WorkPriority.MEDIUM);

        PrioritiesResponse response = await this.GetAsync(owners: [], maxIssues: 1);

        Assert.Equal(expected: 3, actual: response.Priorities.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ZeroOrLessMaxIssuesIsNoCapAsync(int maxIssues)
    {
        this.UpsertIssue("a/r1", 1, WorkPriority.MEDIUM);
        this.UpsertIssue("a/r2", 2, WorkPriority.MEDIUM);
        this.UpsertIssue("b/r3", 3, WorkPriority.MEDIUM);

        PrioritiesResponse response = await this.GetAsync(owners: [], maxIssues: maxIssues);

        Assert.Equal(expected: 3, actual: response.Priorities.Count);
    }

    [Fact]
    public async Task IssueLinkedPullRequestIsPopulatedAsync()
    {
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 5,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: 42,
            assignees: null
        );

        PrioritiesResponse response = await this.GetAsync(owners: []);

        Assert.Equal(expected: 42, actual: Assert.Single(Assert.Single(response.Priorities).LinkedPrNumbers));
    }

    [Fact]
    public async Task BoostedRepoRisesToFrontOfItsBandAsync()
    {
        this.UpsertPr("aa/plain", 1);
        this.UpsertPr("zz/boosted", 2);

        Assert.Equal(
            expected: ["zz/boosted", "aa/plain"],
            actual: await this.GetRepositoriesAsync(owners: [], boostedRepos: ["zz/boosted"])
        );
    }

    [Fact]
    public async Task BoostedRepoNeverRisesAboveUrgentAsync()
    {
        this.UpsertIssue("aa/urgent", 1, WorkPriority.URGENT);
        this.UpsertIssue("zz/boosted", 2, WorkPriority.MEDIUM);

        Assert.Equal(
            expected: ["aa/urgent", "zz/boosted"],
            actual: await this.GetRepositoriesAsync(owners: [], boostedRepos: ["zz/boosted"])
        );
    }

    [Fact]
    public async Task MultipleBoostedReposPreserveConfigOrderAsync()
    {
        this.UpsertPr("aa/one", 1);
        this.UpsertPr("bb/two", 2);
        this.UpsertPr("cc/three", 3);

        Assert.Equal(
            expected: ["cc/three", "aa/one", "bb/two"],
            actual: await this.GetRepositoriesAsync(owners: [], boostedRepos: ["cc/three", "aa/one"])
        );
    }

    [Fact]
    public async Task BandingIsGlobalAcrossOwnersAsync()
    {
        this.UpsertPr("zz-owner/repo", 1);
        this.UpsertIssue("aa-owner/repo", 2, WorkPriority.URGENT);

        Assert.Equal(expected: ["zz-owner/repo", "aa-owner/repo"], actual: await this.GetRepositoriesAsync(owners: []));
    }

    [Fact]
    public async Task EmptyStoreReturnsEmptyListAsync()
    {
        PrioritiesResponse response = await this.GetAsync(owners: []);

        Assert.Empty(response.Priorities);
    }

    [Fact]
    public async Task CloseStaleItemsForRepoAsyncClosesInactiveItemsAsync()
    {
        this.UpsertOpenPullRequest(repository: REPOSITORY, id: 10);
        this.UpsertOpenPullRequest(repository: REPOSITORY, id: 11);
        this.UpsertOpenIssue(repository: REPOSITORY, id: 20);
        this.UpsertOpenIssue(repository: REPOSITORY, id: 21);

        await this._repository.CloseStaleItemsForRepoAsync(
            repository: REPOSITORY,
            activePullRequestNumbers: [10],
            activeIssueNumbers: [20],
            cancellationToken: this.CancellationToken()
        );

        DbStateResponse response = await this.GetAllStateAsync();

        Assert.Equal(
            expected: ["10:Open", "11:Closed"],
            actual: [.. response.PullRequests.Select(static pr => $"{pr.Number}:{pr.Status}")]
        );
        Assert.Equal(
            expected: ["20:Open", "21:Closed"],
            actual: [.. response.Issues.Select(static issue => $"{issue.Number}:{issue.Status}")]
        );
    }

    [Fact]
    public async Task CloseStaleItemsForRepoAsyncWithNoActiveItemsClosesEverythingInTheRepoAsync()
    {
        this.UpsertOpenPullRequest(repository: REPOSITORY, id: 10);
        this.UpsertOpenIssue(repository: REPOSITORY, id: 20);

        await this._repository.CloseStaleItemsForRepoAsync(
            repository: REPOSITORY,
            activePullRequestNumbers: [],
            activeIssueNumbers: [],
            cancellationToken: this.CancellationToken()
        );

        DbStateResponse response = await this.GetAllStateAsync();

        Assert.Equal(expected: "Closed", actual: Assert.Single(response.PullRequests).Status);
        Assert.Equal(expected: "Closed", actual: Assert.Single(response.Issues).Status);
    }

    [Fact]
    public async Task RemoveItemsForRepositoriesAsyncRemovesOnlyNamedRepositoriesAsync()
    {
        this.UpsertOpenPullRequest(repository: REPOSITORY, id: 10);
        this.UpsertOpenPullRequest(repository: OTHER_REPOSITORY, id: 11);
        this.UpsertOpenIssue(repository: REPOSITORY, id: 20);
        this.UpsertOpenIssue(repository: OTHER_REPOSITORY, id: 21);

        await this._repository.RemoveItemsForRepositoriesAsync(
            repositories: [REPOSITORY],
            cancellationToken: this.CancellationToken()
        );

        DbStateResponse response = await this.GetAllStateAsync();

        Assert.Equal(expected: OTHER_REPOSITORY, actual: Assert.Single(response.PullRequests).Repository);
        Assert.Equal(expected: OTHER_REPOSITORY, actual: Assert.Single(response.Issues).Repository);
    }

    private Task<PrioritiesResponse> GetAsync(
        IReadOnlyList<string> owners,
        int maxIssues = 0,
        IReadOnlyList<string>? boostedRepos = null
    )
    {
        return this._repository.GetPrioritisedWorkItemsAsync(
            owners: owners,
            maxIssues: maxIssues,
            boostedRepos: boostedRepos ?? [],
            user: null,
            cancellationToken: this.CancellationToken()
        );
    }

    private async Task<IReadOnlyList<string>> GetRepositoriesAsync(
        IReadOnlyList<string> owners,
        IReadOnlyList<string>? boostedRepos = null
    )
    {
        PrioritiesResponse response = await this.GetAsync(owners: owners, boostedRepos: boostedRepos);

        return [.. response.Priorities.Select(static p => p.Repository)];
    }

    private void UpsertPr(string repository, int id)
    {
        this.UpsertOpenPullRequest(repository: repository, id: id);
    }

    private void UpsertIssue(string repository, int id, WorkPriority priority)
    {
        this._store.UpsertIssue(
            repository: repository,
            id: id,
            status: "Open",
            priority: (int)priority,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: null
        );
    }

    private Task<DbStateResponse> GetAllStateAsync()
    {
        return this._repository.GetDatabaseStateAsync(
            owner: null,
            repo: null,
            user: null,
            cancellationToken: this.CancellationToken()
        );
    }

    private void UpsertOpenPullRequest(string repository, int id)
    {
        this._store.UpsertPullRequest(
            repository: repository,
            id: id,
            status: "Open",
            priority: 1,
            isOnHold: false,
            hasDetail: false,
            commentCount: 0,
            reviewDecision: null,
            failedCheckCount: 0,
            failedCheckNames: null,
            failedCheckSha: null,
            author: null,
            isAdopted: false,
            assignees: null,
            commitAuthors: null
        );
    }

    private void UpsertOpenIssue(string repository, int id)
    {
        this._store.UpsertIssue(
            repository: repository,
            id: id,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: null
        );
    }
}
