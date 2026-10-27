using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database.Rows;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class WorkItemRepositoryTests : TestBase
{
    private static readonly DateTimeOffset BaseTime = MockDateTimeSources.Past.GetUtcNow();

    private readonly TestDatabaseStub _database;
    private readonly TimeProvider _timeProvider;
    private readonly IWorkItemRepository _repository;

    public WorkItemRepositoryTests()
    {
        this._database = new TestDatabaseStub();
        this._timeProvider = MockDateTimeSources.Past;
        this._repository = new WorkItemRepository(this._database, this._timeProvider);
    }

    private void SetupPullRequests(IReadOnlyList<PullRequestRow> rows)
    {
        this._database.SetReturn(rows);
    }

    private void SetupIssues(IReadOnlyList<IssueRow> rows)
    {
        this._database.SetReturn(rows);
    }

    private async Task<IReadOnlyList<WorkItem>> GetItemsAsync(
        IReadOnlyList<string> owners,
        int maxIssues = 0,
        IReadOnlyList<string>? boostedRepos = null
    )
    {
        PrioritiesResponse response = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: owners,
            maxIssues: maxIssues,
            boostedRepos: boostedRepos ?? [],
            user: null,
            cancellationToken: this.CancellationToken()
        );

        return response.Priorities;
    }

    private static PullRequestRow CreatePrRow(
        string repository,
        int id = 1,
        string status = "Open",
        WorkPriority priority = WorkPriority.MEDIUM,
        bool isOnHold = false,
        string? author = null,
        DateTimeOffset? firstSeen = null
    )
    {
        return new PullRequestRow(
            Repository: repository,
            Id: id,
            Status: status,
            FirstSeen: firstSeen ?? BaseTime,
            LastUpdated: BaseTime,
            WhenClosed: null,
            Priority: (int)priority,
            IsOnHold: isOnHold,
            CommentCount: 0,
            ReviewDecision: null,
            FailedCheckCount: 0,
            FailedCheckNames: null,
            FailedCheckSha: null,
            Author: author,
            IsAdopted: false
        );
    }

    private static IssueRow CreateIssueRow(
        string repository,
        int id = 1,
        WorkPriority priority = WorkPriority.MEDIUM,
        bool isOnHold = false,
        int? linkedPrNumber = null,
        DateTimeOffset? firstSeen = null
    )
    {
        return new IssueRow(
            Repository: repository,
            Id: id,
            Status: "Open",
            FirstSeen: firstSeen ?? BaseTime,
            LastUpdated: BaseTime,
            WhenClosed: null,
            Priority: (int)priority,
            IsOnHold: isOnHold,
            LinkedPrNumber: linkedPrNumber
        );
    }

    [Fact]
    public async Task WithNoConfig_OwnersAreOrderedAlphabeticallyAsync()
    {
        this.SetupPullRequests([CreatePrRow("zz-owner/repo", id: 1), CreatePrRow("aa-owner/repo", id: 2)]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "aa-owner/repo", actual: result[0].Repository);
        Assert.Equal(expected: "zz-owner/repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task WithOwnersConfigured_OwnersAreOrderedByConfigAsync()
    {
        this.SetupPullRequests([CreatePrRow("aa-owner/repo", id: 1), CreatePrRow("zz-owner/repo", id: 2)]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: ["zz-owner", "aa-owner"]);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "zz-owner/repo", actual: result[0].Repository);
        Assert.Equal(expected: "aa-owner/repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task PullRequestsAppearBeforeIssues_AcrossDifferentReposAsync()
    {
        this.SetupPullRequests([CreatePrRow("owner/bb-repo", id: 1)]);
        this.SetupIssues([CreateIssueRow("owner/aa-repo", id: 1)]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "PullRequest", actual: result[0].ItemType);
        Assert.Equal(expected: "Issue", actual: result[1].ItemType);
    }

    [Fact]
    public async Task Issues_PerRepo_OnlyHighestPriorityIsReturnedAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([
            CreateIssueRow("owner/repo", id: 1, priority: WorkPriority.LOW),
            CreateIssueRow("owner/repo", id: 2, priority: WorkPriority.HIGH),
            CreateIssueRow("owner/repo", id: 3, priority: WorkPriority.MEDIUM),
        ]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: []);

        WorkItem item = Assert.Single(result);
        Assert.Equal(expected: 2, actual: item.Id);
        Assert.Equal(expected: WorkPriority.HIGH, actual: item.Priority);
    }

    [Fact]
    public async Task Issues_PerRepo_WithEqualPriority_OldestIsReturnedAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([
            CreateIssueRow("owner/repo", id: 2, priority: WorkPriority.MEDIUM, firstSeen: BaseTime.AddHours(2)),
            CreateIssueRow("owner/repo", id: 1, priority: WorkPriority.MEDIUM, firstSeen: BaseTime.AddHours(1)),
        ]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: []);

        WorkItem item = Assert.Single(result);
        Assert.Equal(expected: 1, actual: item.Id);
    }

    [Fact]
    public async Task Issues_EachRepoContributesAtMostOneAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([
            CreateIssueRow("owner/repo", id: 1, priority: WorkPriority.MEDIUM),
            CreateIssueRow("owner/repo", id: 2, priority: WorkPriority.LOW),
        ]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: []);

        _ = Assert.Single(result);
    }

    [Fact]
    public async Task Issues_MaxIssuesCap_LimitsReturnedIssuesAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([
            CreateIssueRow("owner/repo-a", id: 1),
            CreateIssueRow("owner/repo-b", id: 2),
            CreateIssueRow("owner/repo-c", id: 3),
        ]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], maxIssues: 2);

        Assert.Equal(expected: 2, actual: result.Count);
    }

    [Fact]
    public async Task Issues_UrgentPriority_AlwaysAppearsEvenWhenCapReachedAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([
            CreateIssueRow("owner/repo-a", id: 1, priority: WorkPriority.MEDIUM),
            CreateIssueRow("owner/repo-b", id: 2, priority: WorkPriority.MEDIUM),
            CreateIssueRow("owner/repo-c", id: 3, priority: WorkPriority.URGENT),
        ]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], maxIssues: 2);

        Assert.Equal(expected: 3, actual: result.Count);
        Assert.Contains(
            result,
            w =>
                string.Equals(w.Repository, "owner/repo-c", StringComparison.Ordinal)
                && w.Priority == WorkPriority.URGENT
        );
    }

    [Fact]
    public async Task Issues_SecurityPriority_AlwaysAppearsEvenWhenCapReachedAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([
            CreateIssueRow("owner/repo-a", id: 1, priority: WorkPriority.MEDIUM),
            CreateIssueRow("owner/repo-b", id: 2, priority: WorkPriority.MEDIUM),
            CreateIssueRow("owner/repo-c", id: 3, priority: WorkPriority.SECURITY),
        ]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], maxIssues: 2);

        Assert.Equal(expected: 3, actual: result.Count);
        Assert.Contains(
            result,
            w =>
                string.Equals(w.Repository, "owner/repo-c", StringComparison.Ordinal)
                && w.Priority == WorkPriority.SECURITY
        );
    }

    [Fact]
    public async Task Issues_UrgentPriority_IsIncluded_WhenCapReachedAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([
            CreateIssueRow("owner/aa-repo", id: 1, priority: WorkPriority.MEDIUM),
            CreateIssueRow("owner/bb-repo", id: 2, priority: WorkPriority.MEDIUM),
            CreateIssueRow("owner/cc-repo", id: 3, priority: WorkPriority.URGENT),
        ]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], maxIssues: 2);

        Assert.Contains(result, w => string.Equals(w.Repository, "owner/cc-repo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Issues_MaxIssuesCap_DoesNotAffectPullRequestsAsync()
    {
        this.SetupPullRequests([CreatePrRow("owner/repo-a", id: 1), CreatePrRow("owner/repo-b", id: 2)]);
        this.SetupIssues([CreateIssueRow("owner/repo-c", id: 1)]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], maxIssues: 0);

        Assert.Equal(expected: 3, actual: result.Count);
    }

    [Fact]
    public async Task Issues_MaxIssuesCap_AppliesPerOwnerAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([
            CreateIssueRow("aa-owner/repo-a", id: 1),
            CreateIssueRow("aa-owner/repo-b", id: 2),
            CreateIssueRow("aa-owner/repo-c", id: 3),
            CreateIssueRow("bb-owner/repo-a", id: 4),
            CreateIssueRow("bb-owner/repo-b", id: 5),
            CreateIssueRow("bb-owner/repo-c", id: 6),
        ]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: ["aa-owner", "bb-owner"], maxIssues: 2);

        string[] repositories = [.. result.Select(static w => w.Repository)];
        Assert.Equal(
            expected: ["aa-owner/repo-a", "aa-owner/repo-b", "bb-owner/repo-a", "bb-owner/repo-b"],
            actual: repositories
        );
    }

    [Fact]
    public async Task Issues_MaxIssuesCap_KeepsOrderWithinOwnerAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([
            CreateIssueRow("aa-owner/repo-a", id: 1, priority: WorkPriority.LOW),
            CreateIssueRow("aa-owner/repo-b", id: 2, priority: WorkPriority.HIGH),
            CreateIssueRow("aa-owner/repo-c", id: 3, priority: WorkPriority.MEDIUM, firstSeen: BaseTime.AddHours(1)),
            CreateIssueRow("aa-owner/repo-d", id: 4, priority: WorkPriority.MEDIUM),
            CreateIssueRow("bb-owner/repo-a", id: 5, priority: WorkPriority.LOW),
            CreateIssueRow("bb-owner/repo-b", id: 6, priority: WorkPriority.HIGH),
            CreateIssueRow("bb-owner/repo-c", id: 7, priority: WorkPriority.MEDIUM),
        ]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], maxIssues: 2);

        string[] repositories = [.. result.Select(static w => w.Repository)];
        Assert.Equal(
            expected: ["aa-owner/repo-b", "aa-owner/repo-d", "bb-owner/repo-b", "bb-owner/repo-c"],
            actual: repositories
        );
    }

    [Fact]
    public async Task Issues_MaxIssuesCap_GroupsOwnersCaseInsensitivelyAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([
            CreateIssueRow("Owner/repo-a", id: 1),
            CreateIssueRow("owner/repo-b", id: 2),
            CreateIssueRow("OWNER/repo-c", id: 3),
        ]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], maxIssues: 2);

        Assert.Equal(expected: 2, actual: result.Count);
    }

    [Fact]
    public async Task Issues_MaxIssuesCap_UrgentIsUncappedForEveryOwnerAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([
            CreateIssueRow("aa-owner/repo-a", id: 1, priority: WorkPriority.URGENT),
            CreateIssueRow("aa-owner/repo-b", id: 2, priority: WorkPriority.SECURITY),
            CreateIssueRow("aa-owner/repo-c", id: 3),
            CreateIssueRow("aa-owner/repo-d", id: 4),
            CreateIssueRow("bb-owner/repo-a", id: 5, priority: WorkPriority.URGENT),
            CreateIssueRow("bb-owner/repo-b", id: 6, priority: WorkPriority.URGENT),
            CreateIssueRow("bb-owner/repo-c", id: 7),
            CreateIssueRow("bb-owner/repo-d", id: 8),
        ]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], maxIssues: 1);

        Assert.Equal(expected: 6, actual: result.Count);
        Assert.Equal(expected: 4, actual: result.Count(static w => w.Priority >= WorkPriority.URGENT));
        Assert.Equal(
            expected: 1,
            actual: result.Count(static w =>
                w.Priority < WorkPriority.URGENT && w.Repository.StartsWith("aa-owner/", StringComparison.Ordinal)
            )
        );
        Assert.Equal(
            expected: 1,
            actual: result.Count(static w =>
                w.Priority < WorkPriority.URGENT && w.Repository.StartsWith("bb-owner/", StringComparison.Ordinal)
            )
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Issues_MaxIssuesCap_ZeroOrLessIsNoCapForEveryOwnerAsync(int maxIssues)
    {
        this.SetupPullRequests([]);
        this.SetupIssues([
            CreateIssueRow("aa-owner/repo-a", id: 1),
            CreateIssueRow("aa-owner/repo-b", id: 2),
            CreateIssueRow("aa-owner/repo-c", id: 3),
            CreateIssueRow("bb-owner/repo-a", id: 4),
            CreateIssueRow("bb-owner/repo-b", id: 5),
            CreateIssueRow("bb-owner/repo-c", id: 6),
        ]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], maxIssues: maxIssues);

        Assert.Equal(expected: 6, actual: result.Count);
    }

    [Fact]
    public async Task Issues_WhenTotalUnderCap_AllAreReturnedAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([CreateIssueRow("owner/repo-a", id: 1), CreateIssueRow("owner/repo-b", id: 2)]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], maxIssues: 5);

        Assert.Equal(expected: 2, actual: result.Count);
    }

    [Fact]
    public async Task PullRequest_StatusIsReturnedAsync()
    {
        this.SetupPullRequests([CreatePrRow("owner/repo", id: 1, status: "Open")]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: []);

        WorkItem item = Assert.Single(result);
        Assert.Equal(expected: "Open", actual: item.Status);
    }

    [Fact]
    public async Task PullRequest_LinkedPrNumbersIsEmptyAsync()
    {
        this.SetupPullRequests([CreatePrRow("owner/repo", id: 1)]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: []);

        WorkItem item = Assert.Single(result);
        Assert.Empty(item.LinkedPrNumbers);
    }

    [Fact]
    public async Task Issue_LinkedPrNumberIsPopulatedAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([CreateIssueRow("owner/repo", id: 5, linkedPrNumber: 42)]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: []);

        WorkItem item = Assert.Single(result);
        int linkedPr = Assert.Single(item.LinkedPrNumbers);
        Assert.Equal(expected: 42, actual: linkedPr);
    }

    [Fact]
    public async Task EmptyDatabase_ReturnsEmptyListAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: []);

        Assert.Empty(result);
    }

    [Fact]
    public async Task BoostedRepo_RisesToFrontOfItsBandAsync()
    {
        this.SetupPullRequests([CreatePrRow("owner/aa-repo", id: 1), CreatePrRow("owner/zz-repo", id: 2)]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], boostedRepos: ["owner/zz-repo"]);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "owner/zz-repo", actual: result[0].Repository);
        Assert.Equal(expected: "owner/aa-repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task BoostedRepo_NeverRisesAboveAHigherBandAsync()
    {
        this.SetupPullRequests([CreatePrRow("owner/aa-repo", id: 1, priority: WorkPriority.URGENT)]);
        this.SetupIssues([CreateIssueRow("owner/zz-repo", id: 1, priority: WorkPriority.LOW)]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], boostedRepos: ["owner/zz-repo"]);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "PullRequest", actual: result[0].ItemType);
        Assert.Equal(expected: "Issue", actual: result[1].ItemType);
    }

    [Fact]
    public async Task MultipleBoostedRepos_PreserveConfigOrderAsync()
    {
        this.SetupPullRequests([
            CreatePrRow("owner/aa-repo", id: 1),
            CreatePrRow("owner/bb-repo", id: 2),
            CreatePrRow("owner/cc-repo", id: 3),
        ]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            boostedRepos: ["owner/cc-repo", "owner/aa-repo"]
        );

        Assert.Equal(expected: 3, actual: result.Count);
        Assert.Equal(expected: "owner/cc-repo", actual: result[0].Repository);
        Assert.Equal(expected: "owner/aa-repo", actual: result[1].Repository);
        Assert.Equal(expected: "owner/bb-repo", actual: result[2].Repository);
    }

    [Fact]
    public async Task WithEmptyBoostedRepos_OrderingIsUnchangedAsync()
    {
        this.SetupPullRequests([CreatePrRow("zz-owner/repo", id: 1), CreatePrRow("aa-owner/repo", id: 2)]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], boostedRepos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "aa-owner/repo", actual: result[0].Repository);
        Assert.Equal(expected: "zz-owner/repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task UrgentUnboostedItem_StillPrecedesBoostedNonUrgentItemAsync()
    {
        this.SetupPullRequests([
            CreatePrRow("owner/boosted-repo", id: 1, priority: WorkPriority.MEDIUM),
            CreatePrRow("owner/urgent-repo", id: 2, priority: WorkPriority.URGENT),
        ]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], boostedRepos: ["owner/boosted-repo"]);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "owner/urgent-repo", actual: result[0].Repository);
        Assert.Equal(expected: "owner/boosted-repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task RemoveItemsForRepositoriesAsync_WithDuplicateRepos_CallsDatabaseAsync()
    {
        await this._repository.RemoveItemsForRepositoriesAsync(
            repositories: ["owner/repo-a", "Owner/Repo-A", "owner/repo-a"],
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: this._database.VoidExecuteCallCount);
    }

    [Fact]
    public async Task BandingIsGlobalAcrossOwners_NotPerOwnerAsync()
    {
        this.SetupPullRequests([CreatePrRow("zz-owner/repo", id: 1, priority: WorkPriority.MEDIUM)]);
        this.SetupIssues([CreateIssueRow("aa-owner/repo", id: 1, priority: WorkPriority.URGENT)]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], boostedRepos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "zz-owner/repo", actual: result[0].Repository);
        Assert.Equal(expected: "aa-owner/repo", actual: result[1].Repository);
    }

    private static PullRequestStateRow CreatePrStateRow(
        string repository,
        int id = 1,
        string status = "Open",
        bool isOnHold = false,
        DateTimeOffset? lastUpdated = null,
        DateTimeOffset? whenClosed = null,
        string? reviewDecision = null,
        string? failedCheckNames = null,
        string? assignees = null
    )
    {
        return new PullRequestStateRow(
            Repository: repository,
            Id: id,
            Status: status,
            FirstSeen: BaseTime,
            LastUpdated: lastUpdated ?? BaseTime,
            WhenClosed: whenClosed,
            Priority: (int)WorkPriority.HIGH,
            IsOnHold: isOnHold,
            CommentCount: 3,
            ReviewDecision: reviewDecision,
            FailedCheckCount: failedCheckNames is null ? 0 : 2,
            FailedCheckNames: failedCheckNames,
            FailedCheckSha: failedCheckNames is null ? null : "abc123",
            Author: "author",
            IsAdopted: true,
            Assignees: assignees
        );
    }

    private static IssueStateRow CreateIssueStateRow(
        string repository,
        int id = 1,
        string status = "Open",
        bool isOnHold = false,
        DateTimeOffset? lastUpdated = null,
        int? linkedPrNumber = null,
        string? assignees = null
    )
    {
        return new IssueStateRow(
            Repository: repository,
            Id: id,
            Status: status,
            FirstSeen: BaseTime,
            LastUpdated: lastUpdated ?? BaseTime,
            WhenClosed: null,
            Priority: (int)WorkPriority.LOW,
            IsOnHold: isOnHold,
            LinkedPrNumber: linkedPrNumber,
            Assignees: assignees
        );
    }

    private Task<DbStateResponse> GetDatabaseStateAsync(
        IReadOnlyList<PullRequestStateRow> pullRequests,
        IReadOnlyList<IssueStateRow> issues
    )
    {
        this._database.SetReturn(pullRequests);
        this._database.SetReturn(issues);

        return this._repository.GetDatabaseStateAsync(
            owner: null,
            repo: null,
            user: null,
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task GetDatabaseStateWithNoRowsUsesTheClockForAsOfAsync()
    {
        DbStateResponse response = await this.GetDatabaseStateAsync(pullRequests: [], issues: []);

        Assert.Empty(response.PullRequests);
        Assert.Empty(response.Issues);
        Assert.Equal(expected: this._timeProvider.GetUtcNow(), actual: response.AsOf);
    }

    [Fact]
    public async Task GetDatabaseStateAsOfIsTheLatestLastUpdatedAsync()
    {
        DateTimeOffset latest = BaseTime.AddHours(5);

        DbStateResponse response = await this.GetDatabaseStateAsync(
            pullRequests: [CreatePrStateRow("owner/repo", lastUpdated: BaseTime.AddHours(1))],
            issues: [CreateIssueStateRow("owner/repo", lastUpdated: latest)]
        );

        Assert.Equal(expected: latest, actual: response.AsOf);
    }

    [Fact]
    public async Task GetDatabaseStateIncludesClosedAndOnHoldItemsAsync()
    {
        DateTimeOffset closed = BaseTime.AddDays(1);

        DbStateResponse response = await this.GetDatabaseStateAsync(
            pullRequests:
            [
                CreatePrStateRow("owner/repo", id: 1, status: "Closed", whenClosed: closed),
                CreatePrStateRow("owner/repo", id: 2, isOnHold: true),
            ],
            issues: [CreateIssueStateRow("owner/repo", id: 3, status: "Closed", isOnHold: true)]
        );

        Assert.Equal(expected: 2, actual: response.PullRequests.Count);
        Assert.Equal(expected: "Closed", actual: response.PullRequests[0].Status);
        Assert.Equal(expected: closed, actual: response.PullRequests[0].WhenClosed);
        Assert.True(response.PullRequests[1].IsOnHold, userMessage: "On hold PR should be returned");

        IssueState issue = Assert.Single(response.Issues);
        Assert.Equal(expected: "Closed", actual: issue.Status);
        Assert.True(issue.IsOnHold, userMessage: "On hold issue should be returned");
    }

    [Fact]
    public async Task GetDatabaseStateMapsPullRequestDetailAsync()
    {
        DbStateResponse response = await this.GetDatabaseStateAsync(
            pullRequests:
            [
                CreatePrStateRow(
                    "credfeto/credfeto-dispatcher",
                    id: 42,
                    reviewDecision: "Approved",
                    failedCheckNames: "build,lint",
                    assignees: "alice,bob"
                ),
            ],
            issues: []
        );

        PullRequestState pr = Assert.Single(response.PullRequests);
        Assert.Equal(expected: "credfeto", actual: pr.Owner);
        Assert.Equal(expected: "credfeto-dispatcher", actual: pr.Repo);
        Assert.Equal(expected: "credfeto/credfeto-dispatcher", actual: pr.Repository);
        Assert.Equal(expected: 42, actual: pr.Number);
        Assert.Equal(expected: WorkPriority.HIGH, actual: pr.Priority);
        Assert.Equal(expected: ReviewDecisionState.APPROVED, actual: pr.ReviewDecision);
        Assert.Equal(expected: ["alice", "bob"], actual: pr.Assignees);
        Assert.Equal(expected: ["build", "lint"], actual: pr.FailedCheckNames);
        Assert.Equal(expected: 2, actual: pr.FailedCheckCount);
        Assert.Equal(expected: "abc123", actual: pr.FailedCheckSha);
        Assert.Equal(expected: 3, actual: pr.CommentCount);
        Assert.Equal(expected: "author", actual: pr.Author);
        Assert.True(pr.IsAdopted, userMessage: "Adopted flag should be copied");
    }

    [Fact]
    public async Task GetDatabaseStateMapsIssueDetailAsync()
    {
        DbStateResponse response = await this.GetDatabaseStateAsync(
            pullRequests: [],
            issues: [CreateIssueStateRow("owner/repo", id: 7, linkedPrNumber: 9, assignees: "carol")]
        );

        IssueState issue = Assert.Single(response.Issues);
        Assert.Equal(expected: "owner", actual: issue.Owner);
        Assert.Equal(expected: "repo", actual: issue.Repo);
        Assert.Equal(expected: 7, actual: issue.Number);
        Assert.Equal(expected: WorkPriority.LOW, actual: issue.Priority);
        Assert.Equal(expected: 9, actual: issue.LinkedPrNumber);
        Assert.Equal(expected: ["carol"], actual: issue.Assignees);
    }

    [Fact]
    public async Task GetDatabaseStateWithNoAssigneesReturnsEmptyListsAsync()
    {
        DbStateResponse response = await this.GetDatabaseStateAsync(
            pullRequests: [CreatePrStateRow("owner/repo")],
            issues: [CreateIssueStateRow("owner/repo")]
        );

        Assert.Empty(Assert.Single(response.PullRequests).Assignees);
        Assert.Empty(Assert.Single(response.PullRequests).FailedCheckNames);
        Assert.Empty(Assert.Single(response.Issues).Assignees);
        Assert.Equal(expected: ReviewDecisionState.NOT_REVIEWED, actual: response.PullRequests[0].ReviewDecision);
    }
}
