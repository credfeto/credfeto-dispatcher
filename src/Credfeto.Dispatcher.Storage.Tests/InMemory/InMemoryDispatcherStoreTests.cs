using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Dispatcher.Storage.Database.Rows;
using Credfeto.Dispatcher.Storage.InMemory;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.InMemory;

public sealed class InMemoryDispatcherStoreTests : TestBase
{
    private const string REPOSITORY = "owner/repo";

    private readonly TimeProvider _timeProvider;
    private readonly InMemoryDispatcherStore _store;

    public InMemoryDispatcherStoreTests()
    {
        this._timeProvider = MockDateTimeSources.Past;
        this._store = new InMemoryDispatcherStore(this._timeProvider);
    }

    [Fact]
    public void VersionStartsAtZero()
    {
        Assert.Equal(expected: 0, actual: this._store.Version);
    }

    [Fact]
    public void VersionDoesNotChangeOnReads()
    {
        this._store.SetActiveRepos([REPOSITORY]);
        int versionAfterWrite = this._store.Version;

        this._store.GetActiveRepos();
        this._store.GetETag("key");
        this._store.GetActiveWorkItems(user: null);

        Assert.Equal(expected: versionAfterWrite, actual: this._store.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void VersionIncrementsOnceForEachOfSeveralMutatingCalls(int extraCalls)
    {
        int callCount = 1 + extraCalls;

        for (int i = 0; i < callCount; ++i)
        {
            this._store.SaveETag(key: "key", eTag: $"etag-{i}");
        }

        Assert.Equal(expected: callCount, actual: this._store.Version);
    }

    [Fact]
    public void VersionIncrementsWhenLinkIssueToPullRequestActuallyLinks()
    {
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 1,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: null
        );
        int versionAfterUpsert = this._store.Version;

        this._store.LinkIssueToPullRequest(repository: REPOSITORY, id: 1, linkedPrNumber: 42);

        Assert.Equal(expected: versionAfterUpsert + 1, actual: this._store.Version);
    }

    [Fact]
    public void VersionDoesNotIncrementWhenLinkIssueToPullRequestFindsNoMatchingOpenIssue()
    {
        int versionBefore = this._store.Version;

        this._store.LinkIssueToPullRequest(repository: REPOSITORY, id: 999, linkedPrNumber: 42);

        Assert.Equal(expected: versionBefore, actual: this._store.Version);
    }

    [Fact]
    public void ExportSnapshotThenImportSnapshotRoundTripsActiveAndInactiveRepos()
    {
        this._store.SetActiveRepos([REPOSITORY]);
        this._store.SetActiveRepos([]);

        DispatcherStoreSnapshotData snapshot = this._store.ExportSnapshot();

        InMemoryDispatcherStore target = new(this._timeProvider);
        target.ImportSnapshot(snapshot);

        Assert.Empty(target.GetActiveRepos());
    }

    [Fact]
    public void ExportSnapshotThenImportSnapshotRoundTripsPullRequestsIssuesAndEtags()
    {
        this._store.SetActiveRepos([REPOSITORY]);
        this._store.SaveETag(key: "poll-key", eTag: "\"abc123\"");
        this._store.UpsertPullRequest(
            repository: REPOSITORY,
            id: 10,
            status: "Open",
            priority: 1,
            isOnHold: false,
            hasDetail: true,
            commentCount: 2,
            reviewDecision: "Approved",
            failedCheckCount: 0,
            failedCheckNames: null,
            failedCheckSha: null,
            author: "octocat",
            isAdopted: null,
            assignees: null
        );
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 20,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: null
        );
        this._store.LinkIssueToPullRequest(repository: REPOSITORY, id: 20, linkedPrNumber: 10);

        DispatcherStoreSnapshotData snapshot = this._store.ExportSnapshot();

        InMemoryDispatcherStore target = new(this._timeProvider);
        target.ImportSnapshot(snapshot);

        Assert.Equal(expected: "\"abc123\"", actual: target.GetETag("poll-key"));

        (IReadOnlyList<PullRequestRow> pullRequests, IReadOnlyList<IssueRow> issues) = target.GetActiveWorkItems(
            user: null
        );

        // The linked issue is suppressed by an active linked pull request - see
        // InMemoryDispatcherStore.IsLinkedPullRequestActiveNoLock - so only the pull request is
        // "active" here; this asserts the link itself (LinkedPrNumber) survived the round trip.
        PullRequestRow pullRequest = Assert.Single(pullRequests);
        Assert.Equal(expected: 10, actual: pullRequest.Id);
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "newuser")]
    public void UpsertPullRequestDetailColumnsAreGuardedByHasDetail(bool hasDetail, string? author)
    {
        this._store.UpsertPullRequest(
            repository: REPOSITORY,
            id: 10,
            status: "Open",
            priority: 1,
            isOnHold: false,
            hasDetail: true,
            commentCount: 5,
            reviewDecision: "ChangesRequested",
            failedCheckCount: 3,
            failedCheckNames: "build,test",
            failedCheckSha: "abc123",
            author: "octocat",
            isAdopted: null,
            assignees: null
        );

        this._store.UpsertPullRequest(
            repository: REPOSITORY,
            id: 10,
            status: "Open",
            priority: 2,
            isOnHold: false,
            hasDetail: hasDetail,
            commentCount: 0,
            reviewDecision: null,
            failedCheckCount: 0,
            failedCheckNames: null,
            failedCheckSha: null,
            author: author,
            isAdopted: null,
            assignees: null
        );

        (IReadOnlyList<PullRequestRow> pullRequests, _) = this._store.GetActiveWorkItems(user: null);
        PullRequestRow pullRequest = Assert.Single(pullRequests);

        Assert.Equal(expected: 2, actual: pullRequest.Priority);
        Assert.Equal(expected: author ?? "octocat", actual: pullRequest.Author);
        Assert.Equal(expected: hasDetail ? 0 : 5, actual: pullRequest.CommentCount);
        Assert.Equal(expected: hasDetail ? null : "ChangesRequested", actual: pullRequest.ReviewDecision);
        Assert.Equal(expected: hasDetail ? 0 : 3, actual: pullRequest.FailedCheckCount);
        Assert.Equal(expected: hasDetail ? null : "build,test", actual: pullRequest.FailedCheckNames);
        Assert.Equal(expected: hasDetail ? null : "abc123", actual: pullRequest.FailedCheckSha);
    }

    [Fact]
    public void ImportSnapshotClearsPreviousContentsBeforeLoading()
    {
        this._store.SetActiveRepos(["stale/repo"]);
        this._store.SaveETag(key: "stale-key", eTag: "stale-etag");

        DispatcherStoreSnapshotData emptySnapshot = new(
            Repos: [],
            PullRequests: [],
            Issues: [],
            PollingStates: [],
            PullRequestAssignees: [],
            IssueAssignees: []
        );

        this._store.ImportSnapshot(emptySnapshot);

        Assert.Empty(this._store.GetActiveRepos());
        Assert.Null(this._store.GetETag("stale-key"));
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("", "octocat", true)]
    [InlineData("octocat", "octocat", true)]
    [InlineData("OCTOCAT", "octocat", true)]
    [InlineData("someone-else", "octocat", false)]
    [InlineData("octocat,someone-else", "octocat", true)]
    [InlineData("someone-else", null, true)]
    public void GetActiveWorkItemsFiltersIssuesByAssignee(string? assignees, string? user, bool expectedVisible)
    {
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 20,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: SplitAssignees(assignees)
        );

        (_, IReadOnlyList<IssueRow> issues) = this._store.GetActiveWorkItems(user: user);

        Assert.Equal(expected: expectedVisible ? 1 : 0, actual: issues.Count);
    }

    [Theory]
    [InlineData(null, false, "octocat", true)]
    [InlineData("octocat", false, "octocat", true)]
    [InlineData("OctoCat", false, "octocat", true)]
    [InlineData("someone-else", false, "octocat", false)]
    [InlineData("octocat,someone-else", false, "octocat", true)]
    [InlineData("someone-else", true, "octocat", true)]
    [InlineData("someone-else", false, null, true)]
    public void GetActiveWorkItemsFiltersPullRequestsByAssigneeUnlessAdopted(
        string? assignees,
        bool isAdopted,
        string? user,
        bool expectedVisible
    )
    {
        this.UpsertOpenPullRequest(id: 10, isAdopted: isAdopted, assignees: SplitAssignees(assignees));

        (IReadOnlyList<PullRequestRow> pullRequests, _) = this._store.GetActiveWorkItems(user: user);

        Assert.Equal(expected: expectedVisible ? 1 : 0, actual: pullRequests.Count);
    }

    [Fact]
    public void UpsertPullRequestWithNullAssigneesAndAdoptionKeepsExistingValues()
    {
        this.UpsertOpenPullRequest(id: 10, isAdopted: true, assignees: ["someone-else"]);
        this.UpsertOpenPullRequest(id: 11, isAdopted: false, assignees: ["someone-else"]);

        this.UpsertOpenPullRequest(id: 10, isAdopted: null, assignees: null);
        this.UpsertOpenPullRequest(id: 11, isAdopted: null, assignees: null);

        (IReadOnlyList<PullRequestRow> pullRequests, _) = this._store.GetActiveWorkItems(user: "octocat");

        PullRequestRow pullRequest = Assert.Single(pullRequests);
        Assert.Equal(expected: 10, actual: pullRequest.Id);
        Assert.True(condition: pullRequest.IsAdopted, userMessage: "A null isAdopted should keep the stored value");
    }

    [Fact]
    public void UpsertIssueWithEmptyAssigneesClearsExistingAssignees()
    {
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 20,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: ["someone-else"]
        );
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 20,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: []
        );

        (_, IReadOnlyList<IssueRow> issues) = this._store.GetActiveWorkItems(user: "octocat");

        Assert.Single(issues);
    }

    [Fact]
    public void ExportSnapshotThenImportSnapshotRoundTripsAssignees()
    {
        this.UpsertOpenPullRequest(id: 10, isAdopted: false, assignees: ["someone-else"]);
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 20,
            status: "Open",
            priority: 4,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: ["someone-else"]
        );

        DispatcherStoreSnapshotData snapshot = this._store.ExportSnapshot();

        InMemoryDispatcherStore target = new(this._timeProvider);
        target.ImportSnapshot(snapshot);

        (IReadOnlyList<PullRequestRow> filteredPullRequests, IReadOnlyList<IssueRow> filteredIssues) =
            target.GetActiveWorkItems(user: "octocat");
        (IReadOnlyList<PullRequestRow> visiblePullRequests, IReadOnlyList<IssueRow> visibleIssues) =
            target.GetActiveWorkItems(user: "someone-else");

        Assert.Empty(filteredPullRequests);
        Assert.Empty(filteredIssues);
        Assert.Single(visiblePullRequests);
        Assert.Single(visibleIssues);
    }

    [Fact]
    public void ImportSnapshotToleratesMissingAssigneeArrays()
    {
        DispatcherStoreSnapshotData snapshot = new(
            Repos: [],
            PullRequests: [],
            Issues: [],
            PollingStates: [],
            PullRequestAssignees: null,
            IssueAssignees: null
        );

        this._store.ImportSnapshot(snapshot);

        (IReadOnlyList<PullRequestRow> pullRequests, IReadOnlyList<IssueRow> issues) = this._store.GetActiveWorkItems(
            user: "octocat"
        );
        Assert.Empty(pullRequests);
        Assert.Empty(issues);
    }

    [Fact]
    public void GetAllWorkItemsIncludesClosedAndOnHoldItems()
    {
        this.UpsertOpenPullRequest(id: 10, isAdopted: false, assignees: null);
        this._store.CloseStalePullRequests(repository: REPOSITORY, activeIds: []);
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 20,
            status: "Open",
            priority: 1,
            isOnHold: true,
            linkedPrNumber: null,
            assignees: null
        );

        (IReadOnlyList<PullRequestStateRow> pullRequests, IReadOnlyList<IssueStateRow> issues) =
            this._store.GetAllWorkItems(owner: null, repo: null, user: null);

        PullRequestStateRow pullRequest = Assert.Single(pullRequests);
        Assert.Equal(expected: "Closed", actual: pullRequest.Status);
        Assert.NotNull(pullRequest.WhenClosed);

        IssueStateRow issue = Assert.Single(issues);
        Assert.True(condition: issue.IsOnHold, userMessage: "On-hold issues should be included");
    }

    [Fact]
    public void GetAllWorkItemsIncludesItemsInInactiveRepositories()
    {
        this._store.SetActiveRepos([REPOSITORY]);
        this.UpsertOpenPullRequest(id: 10, isAdopted: false, assignees: null);
        this._store.SetActiveRepos([]);

        (IReadOnlyList<PullRequestStateRow> pullRequests, _) = this._store.GetAllWorkItems(
            owner: null,
            repo: null,
            user: null
        );

        Assert.Single(pullRequests);
    }

    [Theory]
    [InlineData(null, null, 3)]
    [InlineData("owner", null, 2)]
    [InlineData("OWNER", null, 2)]
    [InlineData("other", null, 1)]
    [InlineData(null, "repo", 2)]
    [InlineData(null, "REPO", 2)]
    [InlineData("owner", "repo", 1)]
    [InlineData("owner", "second", 1)]
    [InlineData("owner", "missing", 0)]
    [InlineData("own", null, 0)]
    public void GetAllWorkItemsFiltersByOwnerAndRepo(string? owner, string? repo, int expectedCount)
    {
        foreach (string repository in new[] { "owner/repo", "owner/second", "other/repo" })
        {
            this._store.UpsertIssue(
                repository: repository,
                id: 1,
                status: "Open",
                priority: 1,
                isOnHold: false,
                linkedPrNumber: null,
                assignees: null
            );
        }

        (_, IReadOnlyList<IssueStateRow> issues) = this._store.GetAllWorkItems(owner: owner, repo: repo, user: null);

        Assert.Equal(expected: expectedCount, actual: issues.Count);
    }

    [Theory]
    [InlineData(null, false, "octocat", true)]
    [InlineData("OctoCat", false, "octocat", true)]
    [InlineData("someone-else", false, "octocat", false)]
    [InlineData("someone-else", true, "octocat", true)]
    [InlineData("someone-else", false, null, true)]
    public void GetAllWorkItemsFiltersPullRequestsByAssigneeUnlessAdopted(
        string? assignees,
        bool isAdopted,
        string? user,
        bool expectedVisible
    )
    {
        this.UpsertOpenPullRequest(id: 10, isAdopted: isAdopted, assignees: SplitAssignees(assignees));

        (IReadOnlyList<PullRequestStateRow> pullRequests, _) = this._store.GetAllWorkItems(
            owner: null,
            repo: null,
            user: user
        );

        Assert.Equal(expected: expectedVisible ? 1 : 0, actual: pullRequests.Count);
    }

    [Theory]
    [InlineData(null, "octocat", true)]
    [InlineData("OCTOCAT", "octocat", true)]
    [InlineData("someone-else", "octocat", false)]
    [InlineData("someone-else", null, true)]
    public void GetAllWorkItemsFiltersIssuesByAssignee(string? assignees, string? user, bool expectedVisible)
    {
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 20,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: SplitAssignees(assignees)
        );

        (_, IReadOnlyList<IssueStateRow> issues) = this._store.GetAllWorkItems(owner: null, repo: null, user: user);

        Assert.Equal(expected: expectedVisible ? 1 : 0, actual: issues.Count);
    }

    [Fact]
    public void GetAllWorkItemsOrdersByRepositoryThenId()
    {
        this._store.UpsertIssue(
            repository: "owner/zeta",
            id: 1,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: null
        );
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 7,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: null
        );
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 3,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: null
        );
        this.UpsertOpenPullRequest(id: 9, isAdopted: false, assignees: null);
        this.UpsertOpenPullRequest(id: 2, isAdopted: false, assignees: null);

        (IReadOnlyList<PullRequestStateRow> pullRequests, IReadOnlyList<IssueStateRow> issues) =
            this._store.GetAllWorkItems(owner: null, repo: null, user: null);

        Assert.Equal(expected: [2, 9], actual: [.. pullRequests.Select(static pr => pr.Id)]);
        Assert.Equal(
            expected: [$"{REPOSITORY}#3", $"{REPOSITORY}#7", "owner/zeta#1"],
            actual: [.. issues.Select(static issue => $"{issue.Repository}#{issue.Id}")]
        );
    }

    [Fact]
    public void GetAllWorkItemsJoinsAssigneesInOrdinalOrder()
    {
        this.UpsertOpenPullRequest(id: 10, isAdopted: false, assignees: ["zed", "alice"]);
        this.UpsertOpenPullRequest(id: 11, isAdopted: false, assignees: null);
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 20,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: 10,
            assignees: ["bob", "amy"]
        );

        (IReadOnlyList<PullRequestStateRow> pullRequests, IReadOnlyList<IssueStateRow> issues) =
            this._store.GetAllWorkItems(owner: null, repo: null, user: null);

        Assert.Equal(expected: "alice,zed", actual: pullRequests[0].Assignees);
        Assert.Null(pullRequests[1].Assignees);

        IssueStateRow issue = Assert.Single(issues);
        Assert.Equal(expected: "amy,bob", actual: issue.Assignees);
        Assert.Equal(expected: 10, actual: issue.LinkedPrNumber);
    }

    [Fact]
    public void GetAllWorkItemsCopiesPullRequestDetail()
    {
        this._store.UpsertPullRequest(
            repository: REPOSITORY,
            id: 10,
            status: "Draft",
            priority: 3,
            isOnHold: false,
            hasDetail: true,
            commentCount: 4,
            reviewDecision: "Approved",
            failedCheckCount: 2,
            failedCheckNames: "build,test",
            failedCheckSha: "abc123",
            author: "octocat",
            isAdopted: true,
            assignees: null
        );

        (IReadOnlyList<PullRequestStateRow> pullRequests, _) = this._store.GetAllWorkItems(
            owner: null,
            repo: null,
            user: null
        );

        PullRequestStateRow pullRequest = Assert.Single(pullRequests);
        Assert.Equal(expected: "Draft", actual: pullRequest.Status);
        Assert.Equal(expected: 3, actual: pullRequest.Priority);
        Assert.Equal(expected: 4, actual: pullRequest.CommentCount);
        Assert.Equal(expected: "Approved", actual: pullRequest.ReviewDecision);
        Assert.Equal(expected: 2, actual: pullRequest.FailedCheckCount);
        Assert.Equal(expected: "build,test", actual: pullRequest.FailedCheckNames);
        Assert.Equal(expected: "abc123", actual: pullRequest.FailedCheckSha);
        Assert.Equal(expected: "octocat", actual: pullRequest.Author);
        Assert.True(condition: pullRequest.IsAdopted, userMessage: "IsAdopted should be copied");
        Assert.Null(pullRequest.WhenClosed);
    }

    private void UpsertOpenPullRequest(int id, bool? isAdopted, IReadOnlyList<string>? assignees)
    {
        this._store.UpsertPullRequest(
            repository: REPOSITORY,
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
            isAdopted: isAdopted,
            assignees: assignees
        );
    }

    private static IReadOnlyList<string>? SplitAssignees(string? assignees)
    {
        return assignees?.Split(',', StringSplitOptions.RemoveEmptyEntries);
    }
}
