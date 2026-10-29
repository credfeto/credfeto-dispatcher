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

    private Task<PrioritiesResponse> GetPrioritisedAsync(IReadOnlyList<WorkItemRow> rows)
    {
        this._database.SetReturn(rows);

        return this._repository.GetPrioritisedWorkItemsAsync(
            owners: ["owner"],
            maxIssues: 5,
            boostedRepos: ["owner/repo"],
            user: null,
            cancellationToken: this.CancellationToken()
        );
    }

    private static WorkItemRow CreateWorkItemRow(
        string repository,
        int id,
        string itemType,
        WorkPriority priority = WorkPriority.MEDIUM,
        int? linkedPrNumber = null,
        string? reviewDecision = null,
        DateTimeOffset? lastUpdated = null
    )
    {
        return new WorkItemRow(
            Repository: repository,
            Id: id,
            ItemType: itemType,
            Status: "Open",
            FirstSeen: BaseTime,
            LastUpdated: lastUpdated ?? BaseTime,
            WhenClosed: null,
            Priority: (int)priority,
            IsOnHold: false,
            LinkedPrNumber: linkedPrNumber,
            CommentCount: 0,
            ReviewDecision: reviewDecision,
            FailedCheckCount: 0,
            FailedCheckNames: null,
            FailedCheckSha: null,
            Author: null
        );
    }

    [Fact]
    public async Task GetPrioritisedPreservesDatabaseOrderAsync()
    {
        PrioritiesResponse response = await this.GetPrioritisedAsync([
            CreateWorkItemRow("zz/repo", id: 1, itemType: "PullRequest"),
            CreateWorkItemRow("aa/repo", id: 2, itemType: "Issue"),
            CreateWorkItemRow("mm/repo", id: 3, itemType: "PullRequest"),
        ]);

        Assert.Equal(
            expected: ["zz/repo", "aa/repo", "mm/repo"],
            actual: [.. response.Priorities.Select(static p => p.Repository)]
        );
    }

    [Fact]
    public async Task GetPrioritisedMapsPullRequestRowAsync()
    {
        PrioritiesResponse response = await this.GetPrioritisedAsync([
            CreateWorkItemRow("owner/repo", id: 1, itemType: "PullRequest", reviewDecision: "Approved"),
        ]);

        WorkItem item = Assert.Single(response.Priorities);
        Assert.Equal(expected: "PullRequest", actual: item.ItemType);
        Assert.Equal(expected: ReviewDecisionState.APPROVED, actual: item.ReviewDecision);
        Assert.Empty(item.LinkedPrNumbers);
    }

    [Fact]
    public async Task GetPrioritisedMapsIssueRowWithLinkedPullRequestAsync()
    {
        PrioritiesResponse response = await this.GetPrioritisedAsync([
            CreateWorkItemRow("owner/repo", id: 5, itemType: "Issue", linkedPrNumber: 42),
        ]);

        WorkItem item = Assert.Single(response.Priorities);
        Assert.Equal(expected: "Issue", actual: item.ItemType);
        Assert.Equal(expected: ReviewDecisionState.NOT_APPLICABLE, actual: item.ReviewDecision);
        Assert.Equal(expected: 42, actual: Assert.Single(item.LinkedPrNumbers));
    }

    [Fact]
    public async Task GetPrioritisedWithNoRowsUsesTheClockForAsOfAsync()
    {
        PrioritiesResponse response = await this.GetPrioritisedAsync([]);

        Assert.Empty(response.Priorities);
        Assert.Equal(expected: this._timeProvider.GetUtcNow(), actual: response.AsOf);
        Assert.Equal(expected: 0, actual: response.LagSeconds);
    }

    [Fact]
    public async Task GetPrioritisedAsOfIsTheLatestLastUpdatedAsync()
    {
        DateTimeOffset latest = BaseTime.AddSeconds(-30);

        PrioritiesResponse response = await this.GetPrioritisedAsync([
            CreateWorkItemRow("owner/a", id: 1, itemType: "PullRequest", lastUpdated: BaseTime.AddMinutes(-5)),
            CreateWorkItemRow("owner/b", id: 2, itemType: "Issue", lastUpdated: latest),
        ]);

        Assert.Equal(expected: latest, actual: response.AsOf);
        Assert.Equal(expected: 30, actual: response.LagSeconds);
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

    private static PullRequestStateRow CreatePrStateRow(
        string repository,
        int id = 1,
        string status = "Open",
        bool isOnHold = false,
        DateTimeOffset? lastUpdated = null,
        DateTimeOffset? whenClosed = null,
        string? reviewDecision = null,
        string? failedCheckNames = null,
        string? assignees = null,
        int? failedCheckCount = null
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
            FailedCheckCount: failedCheckCount ?? (failedCheckNames is null ? 0 : 2),
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
    public async Task GetDatabaseStateRoundTripsCommaBearingFailedCheckNameAsync()
    {
        PullRequestRun[] runs =
        [
            new(
                Name: "build (ubuntu-latest, net8.0)",
                Status: "completed",
                Conclusion: "failure",
                Url: new("https://github.com/owner/repo/actions/runs/1"),
                IsRequired: true,
                HeadSha: "abc123"
            ),
            new(
                Name: "lint",
                Status: "completed",
                Conclusion: "success",
                Url: new("https://github.com/owner/repo/actions/runs/2"),
                IsRequired: true,
                HeadSha: "abc123"
            ),
        ];

        DbStateResponse response = await this.GetDatabaseStateAsync(
            pullRequests:
            [
                CreatePrStateRow(
                    "owner/repo",
                    failedCheckNames: NotificationDetailMapping.BuildFailedCheckNames(runs),
                    failedCheckCount: NotificationDetailMapping.CountFailedChecks(runs)
                ),
            ],
            issues: []
        );

        PullRequestState pr = Assert.Single(response.PullRequests);
        Assert.Equal(expected: "build (ubuntu-latest, net8.0)", actual: Assert.Single(pr.FailedCheckNames));
        Assert.Equal(expected: pr.FailedCheckCount, actual: pr.FailedCheckNames.Length);
    }

    [Theory]
    [InlineData("[\"build\",\"test (a, b)\"]", "build", "test (a, b)")]
    [InlineData("build,test", "build", "test")]
    [InlineData("[lint],test", "[lint]", "test")]
    public async Task GetDatabaseStateParsesStoredFailedCheckNamesAsync(
        string stored,
        string expectedFirst,
        string expectedSecond
    )
    {
        DbStateResponse response = await this.GetDatabaseStateAsync(
            pullRequests: [CreatePrStateRow("owner/repo", failedCheckNames: stored)],
            issues: []
        );

        PullRequestState pr = Assert.Single(response.PullRequests);
        Assert.Equal(expected: [expectedFirst, expectedSecond], actual: pr.FailedCheckNames);
        Assert.Equal(expected: pr.FailedCheckCount, actual: pr.FailedCheckNames.Length);
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
