using System;
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
            assignees: null
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
