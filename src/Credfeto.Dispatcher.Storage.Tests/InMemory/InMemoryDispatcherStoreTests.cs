using System.Collections.Generic;
using Credfeto.Dispatcher.Storage.Database.Rows;
using Credfeto.Dispatcher.Storage.InMemory;
using FunFair.Test.Common;
using FunFair.Test.Common.Mocks;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.InMemory;

public sealed class InMemoryDispatcherStoreTests : TestBase
{
    private const string REPOSITORY = "owner/repo";

    private readonly FakeTimeProvider _timeProvider;
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
        this._store.GetActiveWorkItems();

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
            linkedPrNumber: null
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
            author: "octocat"
        );
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 20,
            status: "Open",
            priority: 1,
            isOnHold: false,
            linkedPrNumber: null
        );
        this._store.LinkIssueToPullRequest(repository: REPOSITORY, id: 20, linkedPrNumber: 10);

        DispatcherStoreSnapshotData snapshot = this._store.ExportSnapshot();

        InMemoryDispatcherStore target = new(this._timeProvider);
        target.ImportSnapshot(snapshot);

        Assert.Equal(expected: "\"abc123\"", actual: target.GetETag("poll-key"));

        (IReadOnlyList<PullRequestRow> pullRequests, IReadOnlyList<IssueRow> issues) = target.GetActiveWorkItems();

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
            author: "octocat"
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
            author: author
        );

        (IReadOnlyList<PullRequestRow> pullRequests, _) = this._store.GetActiveWorkItems();
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

        DispatcherStoreSnapshotData emptySnapshot = new(Repos: [], PullRequests: [], Issues: [], PollingStates: []);

        this._store.ImportSnapshot(emptySnapshot);

        Assert.Empty(this._store.GetActiveRepos());
        Assert.Null(this._store.GetETag("stale-key"));
    }
}
