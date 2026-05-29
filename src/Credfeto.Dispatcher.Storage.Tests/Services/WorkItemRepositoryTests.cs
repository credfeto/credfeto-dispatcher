using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database.Rows;
using FunFair.Test.Common;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class WorkItemRepositoryTests : TestBase
{
    private static readonly DateTimeOffset BaseTime = new(
        year: 2025,
        month: 1,
        day: 1,
        hour: 0,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    private readonly TestDatabaseStub _database;
    private readonly FakeTimeProvider _timeProvider;
    private readonly IWorkItemRepository _repository;

    public WorkItemRepositoryTests()
    {
        this._database = new TestDatabaseStub();
        this._timeProvider = new FakeTimeProvider(BaseTime);
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
        IReadOnlyList<string> repos,
        TimeSpan stuckDependabotTimeout = default,
        int maxIssues = 0
    )
    {
        PrioritiesResponse response = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: owners,
            repos: repos,
            stuckDependabotTimeout: stuckDependabotTimeout,
            maxIssues: maxIssues,
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
            Author: author
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

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "aa-owner/repo", actual: result[0].Repository);
        Assert.Equal(expected: "zz-owner/repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task WithOwnersConfigured_OwnersAreOrderedByConfigAsync()
    {
        this.SetupPullRequests([CreatePrRow("aa-owner/repo", id: 1), CreatePrRow("zz-owner/repo", id: 2)]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: ["zz-owner", "aa-owner"], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "zz-owner/repo", actual: result[0].Repository);
        Assert.Equal(expected: "aa-owner/repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task WithNoConfig_ReposAreOrderedAlphabeticallyAsync()
    {
        this.SetupPullRequests([CreatePrRow("owner/zz-repo", id: 1), CreatePrRow("owner/aa-repo", id: 2)]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "owner/aa-repo", actual: result[0].Repository);
        Assert.Equal(expected: "owner/zz-repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task WithReposConfigured_ReposAreOrderedByConfigAsync()
    {
        this.SetupPullRequests([CreatePrRow("owner/aa-repo", id: 1), CreatePrRow("owner/zz-repo", id: 2)]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: ["owner/zz-repo", "owner/aa-repo"]
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "owner/zz-repo", actual: result[0].Repository);
        Assert.Equal(expected: "owner/aa-repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task PullRequestsAppearBeforeIssues_AcrossDifferentReposAsync()
    {
        this.SetupPullRequests([CreatePrRow("owner/bb-repo", id: 1)]);
        this.SetupIssues([CreateIssueRow("owner/aa-repo", id: 1)]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "PullRequest", actual: result[0].ItemType);
        Assert.Equal(expected: "Issue", actual: result[1].ItemType);
    }

    [Fact]
    public async Task Issues_PerRepo_OnlyHighestPriorityIsReturnedAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues(
            [
                CreateIssueRow("owner/repo", id: 1, priority: WorkPriority.LOW),
                CreateIssueRow("owner/repo", id: 2, priority: WorkPriority.HIGH),
                CreateIssueRow("owner/repo", id: 3, priority: WorkPriority.MEDIUM),
            ]
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem item = Assert.Single(result);
        Assert.Equal(expected: 2, actual: item.Id);
        Assert.Equal(expected: WorkPriority.HIGH, actual: item.Priority);
    }

    [Fact]
    public async Task Issues_PerRepo_WithEqualPriority_OldestIsReturnedAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues(
            [
                CreateIssueRow("owner/repo", id: 2, priority: WorkPriority.MEDIUM, firstSeen: BaseTime.AddHours(2)),
                CreateIssueRow("owner/repo", id: 1, priority: WorkPriority.MEDIUM, firstSeen: BaseTime.AddHours(1)),
            ]
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem item = Assert.Single(result);
        Assert.Equal(expected: 1, actual: item.Id);
    }

    [Fact]
    public async Task Issues_EachRepoContributesAtMostOneAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues(
            [
                CreateIssueRow("owner/repo", id: 1, priority: WorkPriority.MEDIUM),
                CreateIssueRow("owner/repo", id: 2, priority: WorkPriority.LOW),
            ]
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        _ = Assert.Single(result);
    }

    [Fact]
    public async Task Issues_MaxIssuesCap_LimitsReturnedIssuesAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues(
            [
                CreateIssueRow("owner/repo-a", id: 1),
                CreateIssueRow("owner/repo-b", id: 2),
                CreateIssueRow("owner/repo-c", id: 3),
            ]
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: [], maxIssues: 2);

        Assert.Equal(expected: 2, actual: result.Count);
    }

    [Fact]
    public async Task Issues_UrgentPriority_AlwaysAppearsEvenWhenCapReachedAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues(
            [
                CreateIssueRow("owner/repo-a", id: 1, priority: WorkPriority.MEDIUM),
                CreateIssueRow("owner/repo-b", id: 2, priority: WorkPriority.MEDIUM),
                CreateIssueRow("owner/repo-c", id: 3, priority: WorkPriority.URGENT),
            ]
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: [], maxIssues: 2);

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
        this.SetupIssues(
            [
                CreateIssueRow("owner/repo-a", id: 1, priority: WorkPriority.MEDIUM),
                CreateIssueRow("owner/repo-b", id: 2, priority: WorkPriority.MEDIUM),
                CreateIssueRow("owner/repo-c", id: 3, priority: WorkPriority.SECURITY),
            ]
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: [], maxIssues: 2);

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
        this.SetupIssues(
            [
                CreateIssueRow("owner/aa-repo", id: 1, priority: WorkPriority.MEDIUM),
                CreateIssueRow("owner/bb-repo", id: 2, priority: WorkPriority.MEDIUM),
                CreateIssueRow("owner/cc-repo", id: 3, priority: WorkPriority.URGENT),
            ]
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: [], maxIssues: 2);

        Assert.Contains(result, w => string.Equals(w.Repository, "owner/cc-repo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Issues_MaxIssuesCap_DoesNotAffectPullRequestsAsync()
    {
        this.SetupPullRequests([CreatePrRow("owner/repo-a", id: 1), CreatePrRow("owner/repo-b", id: 2)]);
        this.SetupIssues([CreateIssueRow("owner/repo-c", id: 1)]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: [], maxIssues: 0);

        Assert.Equal(expected: 3, actual: result.Count);
    }

    [Fact]
    public async Task Issues_WhenTotalUnderCap_AllAreReturnedAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([CreateIssueRow("owner/repo-a", id: 1), CreateIssueRow("owner/repo-b", id: 2)]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: [], maxIssues: 5);

        Assert.Equal(expected: 2, actual: result.Count);
    }

    [Fact]
    public async Task PullRequest_StatusIsReturnedAsync()
    {
        this.SetupPullRequests([CreatePrRow("owner/repo", id: 1, status: "Open")]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem item = Assert.Single(result);
        Assert.Equal(expected: "Open", actual: item.Status);
    }

    [Fact]
    public async Task PullRequest_LinkedPrNumbersIsEmptyAsync()
    {
        this.SetupPullRequests([CreatePrRow("owner/repo", id: 1)]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem item = Assert.Single(result);
        Assert.Empty(item.LinkedPrNumbers);
    }

    [Fact]
    public async Task StuckDependabot_IsUpgradedToSecurityPriorityAsync()
    {
        DateTimeOffset oldFirstSeen = BaseTime.AddDays(-10);
        this.SetupPullRequests(
            [
                CreatePrRow(
                    "owner/repo",
                    id: 1,
                    priority: WorkPriority.LOW,
                    author: "dependabot[bot]",
                    firstSeen: oldFirstSeen
                ),
            ]
        );
        this.SetupIssues([]);

        TimeSpan stuckTimeout = TimeSpan.FromDays(7);
        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: [],
            stuckDependabotTimeout: stuckTimeout
        );

        WorkItem item = Assert.Single(result);
        Assert.Equal(expected: WorkPriority.SECURITY, actual: item.Priority);
    }

    [Fact]
    public async Task RecentDependabot_IsNotUpgradedToSecurityPriorityAsync()
    {
        this.SetupPullRequests(
            [
                CreatePrRow(
                    "owner/repo",
                    id: 1,
                    priority: WorkPriority.LOW,
                    author: "dependabot[bot]",
                    firstSeen: BaseTime.AddDays(-1)
                ),
            ]
        );
        this.SetupIssues([]);

        TimeSpan stuckTimeout = TimeSpan.FromDays(7);
        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: [],
            stuckDependabotTimeout: stuckTimeout
        );

        WorkItem item = Assert.Single(result);
        Assert.Equal(expected: WorkPriority.LOW, actual: item.Priority);
    }

    [Fact]
    public async Task NonDependabotPr_IsNotUpgradedToSecurityPriorityAsync()
    {
        this.SetupPullRequests(
            [
                CreatePrRow(
                    "owner/repo",
                    id: 1,
                    priority: WorkPriority.LOW,
                    author: "some-dev",
                    firstSeen: BaseTime.AddDays(-10)
                ),
            ]
        );
        this.SetupIssues([]);

        TimeSpan stuckTimeout = TimeSpan.FromDays(7);
        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: [],
            stuckDependabotTimeout: stuckTimeout
        );

        WorkItem item = Assert.Single(result);
        Assert.Equal(expected: WorkPriority.LOW, actual: item.Priority);
    }

    [Fact]
    public async Task Issue_LinkedPrNumberIsPopulatedAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([CreateIssueRow("owner/repo", id: 5, linkedPrNumber: 42)]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem item = Assert.Single(result);
        int linkedPr = Assert.Single(item.LinkedPrNumbers);
        Assert.Equal(expected: 42, actual: linkedPr);
    }

    [Fact]
    public async Task EmptyDatabase_ReturnsEmptyListAsync()
    {
        this.SetupPullRequests([]);
        this.SetupIssues([]);

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Empty(result);
    }
}
