using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Entities;
using FunFair.Test.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class WorkItemRepositoryTests : TestBase, IAsyncLifetime
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

    private readonly SqliteConnection _connection;
    private readonly DispatcherDbContext _seedContext;
    private readonly FakeTimeProvider _timeProvider;
    private readonly IWorkItemRepository _repository;

    public WorkItemRepositoryTests()
    {
        this._connection = new SqliteConnection("DataSource=:memory:");
        this._connection.Open();

        DbContextOptions<DispatcherDbContext> options = new DbContextOptionsBuilder<DispatcherDbContext>()
            .UseSqlite(this._connection)
            .Options;

        using (DispatcherDbContext ctx = new(options))
        {
            ctx.Database.EnsureCreated();
        }

        TestDbContextFactory factory = new(options);
        this._seedContext = new DispatcherDbContext(options);
        this._timeProvider = new FakeTimeProvider(BaseTime);
        this._repository = new WorkItemRepository(factory, this._timeProvider);
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await this._seedContext.DisposeAsync();
        await this._connection.DisposeAsync();
    }

    [Fact]
    public async Task WithNoConfig_OwnersAreOrderedAlphabeticallyAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("zz-owner/repo", id: 1));
        this._seedContext.PullRequests.Add(CreatePr("aa-owner/repo", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "aa-owner/repo", actual: result[0].Repository);
        Assert.Equal(expected: "zz-owner/repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task WithOwnersConfigured_OwnersAreOrderedByConfigAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("aa-owner/repo", id: 1));
        this._seedContext.PullRequests.Add(CreatePr("zz-owner/repo", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: ["zz-owner", "aa-owner"], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "zz-owner/repo", actual: result[0].Repository);
        Assert.Equal(expected: "aa-owner/repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task WithNoConfig_ReposAreOrderedAlphabeticallyAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/zz-repo", id: 1));
        this._seedContext.PullRequests.Add(CreatePr("owner/aa-repo", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "owner/aa-repo", actual: result[0].Repository);
        Assert.Equal(expected: "owner/zz-repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task WithReposConfigured_ReposAreOrderedByConfigAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/aa-repo", id: 1));
        this._seedContext.PullRequests.Add(CreatePr("owner/zz-repo", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: ["owner/zz-repo", "owner/aa-repo"]
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "owner/zz-repo", actual: result[0].Repository);
        Assert.Equal(expected: "owner/aa-repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task IssueInRepoWithOpenPr_IsExcludedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 10));
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 20));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: "PullRequest", actual: single.ItemType);
        Assert.Equal(expected: 20, actual: single.Id);
    }

    [Fact]
    public async Task PullRequestsAppearBeforeIssues_AcrossDifferentReposAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/zz-repo", id: 10));
        this._seedContext.PullRequests.Add(CreatePr("owner/aa-repo", id: 20));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "PullRequest", actual: result[0].ItemType);
        Assert.Equal(expected: "Issue", actual: result[1].ItemType);
    }

    [Fact]
    public async Task TypeOrderingTakesPrecedenceOverRepoOrderingAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/aa-repo", id: 1));
        this._seedContext.PullRequests.Add(CreatePr("owner/zz-repo", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "PullRequest", actual: result[0].ItemType);
        Assert.Equal(expected: "Issue", actual: result[1].ItemType);
    }

    [Fact]
    public async Task Issues_AcrossRepos_AreOrderedByPriorityDescendingAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/aa-repo", id: 1, priority: WorkPriority.URGENT));
        this._seedContext.Issues.Add(CreateIssue("owner/bb-repo", id: 2, priority: WorkPriority.HIGH));
        this._seedContext.Issues.Add(CreateIssue("owner/cc-repo", id: 3, priority: WorkPriority.MEDIUM));
        this._seedContext.Issues.Add(CreateIssue("owner/dd-repo", id: 4, priority: WorkPriority.LOW));
        this._seedContext.Issues.Add(CreateIssue("owner/ee-repo", id: 5, priority: WorkPriority.UNKNOWN));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: ["owner/aa-repo", "owner/bb-repo", "owner/cc-repo", "owner/dd-repo", "owner/ee-repo"]
        );

        Assert.Equal(expected: 5, actual: result.Count);
        Assert.Equal(expected: WorkPriority.URGENT, actual: result[0].Priority);
        Assert.Equal(expected: WorkPriority.HIGH, actual: result[1].Priority);
        Assert.Equal(expected: WorkPriority.MEDIUM, actual: result[2].Priority);
        Assert.Equal(expected: WorkPriority.LOW, actual: result[3].Priority);
        Assert.Equal(expected: WorkPriority.UNKNOWN, actual: result[4].Priority);
    }

    [Fact]
    public async Task Issues_PerRepo_OnlyHighestPriorityIsReturnedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1, priority: WorkPriority.UNKNOWN));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 2, priority: WorkPriority.LOW));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 3, priority: WorkPriority.URGENT));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: WorkPriority.URGENT, actual: single.Priority);
        Assert.Equal(expected: 3, actual: single.Id);
    }

    [Fact]
    public async Task Issues_PerRepo_WithEqualPriority_OldestIsReturnedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1, firstSeen: BaseTime.AddDays(1)));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 2, firstSeen: BaseTime));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 2, actual: single.Id);
    }

    [Fact]
    public async Task Issues_AcrossRepos_UntaggedAppearAfterLowPriorityAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/aa-repo", id: 1, priority: WorkPriority.UNKNOWN));
        this._seedContext.Issues.Add(CreateIssue("owner/bb-repo", id: 2, priority: WorkPriority.LOW));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: ["owner/bb-repo", "owner/aa-repo"]
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: WorkPriority.LOW, actual: result[0].Priority);
        Assert.Equal(expected: WorkPriority.UNKNOWN, actual: result[1].Priority);
    }

    [Fact]
    public async Task ClosedPullRequestsAreExcludedAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1, status: "Closed"));
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 2, actual: single.Id);
    }

    [Fact]
    public async Task OnHoldIssuesAreExcludedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1, isOnHold: true));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 2, actual: single.Id);
    }

    [Fact]
    public async Task IssuesInRepoWithOpenPrAreAllExcludedAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 42, status: "Open"));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1, linkedPrNumber: 42));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        // PR 42 (Open) is included; both issues excluded because repo has an open PR
        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: "PullRequest", actual: single.ItemType);
        Assert.Equal(expected: 42, actual: single.Id);
    }

    [Fact]
    public async Task UrgentIssueInRepoWithOpenPr_IsIncludedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 10, priority: WorkPriority.URGENT));
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 20));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Contains(result, w => string.Equals(w.ItemType, "Issue", StringComparison.Ordinal) && w.Id == 10);
        Assert.Contains(result, w => string.Equals(w.ItemType, "PullRequest", StringComparison.Ordinal) && w.Id == 20);
    }

    [Fact]
    public async Task SecurityIssueInRepoWithOpenPr_IsIncludedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 10, priority: WorkPriority.SECURITY));
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 20));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Contains(result, w => string.Equals(w.ItemType, "Issue", StringComparison.Ordinal) && w.Id == 10);
        Assert.Contains(result, w => string.Equals(w.ItemType, "PullRequest", StringComparison.Ordinal) && w.Id == 20);
    }

    [Fact]
    public async Task HighPriorityIssueInRepoWithOpenPr_IsExcludedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 10, priority: WorkPriority.HIGH));
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 20));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: "PullRequest", actual: single.ItemType);
        Assert.Equal(expected: 20, actual: single.Id);
    }

    [Fact]
    public async Task UrgentIssueInRepoWithDraftPr_IsIncludedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 10, priority: WorkPriority.URGENT));
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 20, status: "Draft"));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Contains(result, w => string.Equals(w.ItemType, "Issue", StringComparison.Ordinal) && w.Id == 10);
        Assert.Contains(result, w => string.Equals(w.ItemType, "PullRequest", StringComparison.Ordinal) && w.Id == 20);
    }

    [Fact]
    public async Task LowPriorityIssueInRepoWithDraftPr_IsExcludedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 10, priority: WorkPriority.LOW));
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 20, status: "Draft"));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: "PullRequest", actual: single.ItemType);
        Assert.Equal(expected: 20, actual: single.Id);
    }

    [Fact]
    public async Task IssueWithClosedLinkedPrIsIncludedAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 42, status: "Closed"));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1, linkedPrNumber: 42));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        // PR 42 is Closed (excluded from PR results); Issue 1 is included because linked PR is not open
        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 1, actual: single.Id);
    }

    [Fact]
    public async Task IssueWithLinkedPrNotInDbIsIncludedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1, linkedPrNumber: 99));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 1, actual: single.Id);
    }

    [Fact]
    public async Task PullRequest_StatusIsReturnedAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1, status: "Open"));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: "Open", actual: single.Status);
    }

    [Fact]
    public async Task PullRequest_WhenClosedIsReturnedAsync()
    {
        DateTimeOffset closedAt = BaseTime.AddDays(1);
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1, whenClosed: closedAt));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: closedAt, actual: single.WhenClosed);
    }

    [Fact]
    public async Task PullRequest_IsOnHoldIsFalseForIncludedItemsAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.False(single.IsOnHold, userMessage: "IsOnHold should be false for non-on-hold pull requests");
    }

    [Fact]
    public async Task PullRequest_LinkedPrNumbersIsEmptyAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Empty(single.LinkedPrNumbers);
    }

    [Fact]
    public async Task Issue_StatusIsReturnedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: "Open", actual: single.Status);
    }

    [Fact]
    public async Task Issue_WhenClosedIsNullForOpenItemsAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Null(single.WhenClosed);
    }

    [Fact]
    public async Task Issue_IsOnHoldIsFalseForIncludedItemsAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.False(single.IsOnHold, userMessage: "IsOnHold should be false for non-on-hold issues");
    }

    [Fact]
    public async Task Issue_LinkedPrNumbersIsEmptyForUnlinkedItemsAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Empty(single.LinkedPrNumbers);
    }

    [Fact]
    public async Task PullRequest_CommentCountIsReturnedAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1, commentCount: 5));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 5, actual: single.CommentCount);
    }

    [Fact]
    public async Task PullRequest_ReviewDecisionIsReturnedWhenSetAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1, reviewDecision: "ChangesRequested"));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: ReviewDecisionState.CHANGES_REQUESTED, actual: single.ReviewDecision);
    }

    [Fact]
    public async Task PullRequest_ReviewDecisionIsNotReviewedWhenNotSetAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: ReviewDecisionState.NOT_REVIEWED, actual: single.ReviewDecision);
    }

    [Fact]
    public async Task PullRequest_FailedCheckCountIsReturnedAsync()
    {
        this._seedContext.PullRequests.Add(
            CreatePr("owner/repo", id: 1, failedCheckCount: 2, failedCheckNames: "build,lint")
        );
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 2, actual: single.FailedCheckCount);
        ImmutableArray<string> expectedNames = ["build", "lint"];
        Assert.Equal(expected: expectedNames, actual: single.FailedCheckNames);
    }

    [Fact]
    public async Task PullRequest_FailedCheckCountIsZeroWhenNoFailuresAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 0, actual: single.FailedCheckCount);
        Assert.Empty(single.FailedCheckNames);
    }

    [Fact]
    public async Task Issue_CommentCountIsZeroAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 0, actual: single.CommentCount);
    }

    [Fact]
    public async Task Issue_ReviewDecisionIsNotApplicableAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: ReviewDecisionState.NOT_APPLICABLE, actual: single.ReviewDecision);
    }

    [Fact]
    public async Task Issue_FailedCheckCountIsZeroAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 0, actual: single.FailedCheckCount);
        Assert.Empty(single.FailedCheckNames);
    }

    [Fact]
    public async Task StuckDependabotPullRequestAtThresholdIsRaisedToSecurityPriorityAsync()
    {
        this._seedContext.PullRequests.Add(
            CreatePr("owner/repo", id: 1, firstSeen: BaseTime, priority: WorkPriority.LOW, author: "dependabot[bot]")
        );
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        this._timeProvider.SetUtcNow(BaseTime.AddHours(3));

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: [],
            stuckDependabotTimeout: TimeSpan.FromHours(3)
        );

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: WorkPriority.SECURITY, actual: single.Priority);
    }

    [Fact]
    public async Task StuckDependabotPullRequestBelowThresholdKeepsOriginalPriorityAsync()
    {
        this._seedContext.PullRequests.Add(
            CreatePr("owner/repo", id: 1, firstSeen: BaseTime, priority: WorkPriority.LOW, author: "dependabot[bot]")
        );
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        this._timeProvider.SetUtcNow(BaseTime.AddHours(2).AddMinutes(59));

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: [],
            stuckDependabotTimeout: TimeSpan.FromHours(3)
        );

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: WorkPriority.LOW, actual: single.Priority);
    }

    [Fact]
    public async Task StuckPullRequestFromNonDependabotAuthorKeepsOriginalPriorityAsync()
    {
        this._seedContext.PullRequests.Add(
            CreatePr("owner/repo", id: 1, firstSeen: BaseTime, priority: WorkPriority.LOW, author: "someone-else")
        );
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        this._timeProvider.SetUtcNow(BaseTime.AddDays(7));

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: [],
            stuckDependabotTimeout: TimeSpan.FromHours(3)
        );

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: WorkPriority.LOW, actual: single.Priority);
    }

    [Fact]
    public async Task StuckDependabotRuleIsDisabledWhenTimeoutIsZeroAsync()
    {
        this._seedContext.PullRequests.Add(
            CreatePr("owner/repo", id: 1, firstSeen: BaseTime, priority: WorkPriority.LOW, author: "dependabot[bot]")
        );
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        this._timeProvider.SetUtcNow(BaseTime.AddDays(7));

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: [],
            stuckDependabotTimeout: TimeSpan.Zero
        );

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: WorkPriority.LOW, actual: single.Priority);
    }

    [Fact]
    public async Task PullRequestAuthorIsReturnedAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1, author: "dependabot[bot]"));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: "dependabot[bot]", actual: single.Author);
    }

    [Fact]
    public async Task AsOf_IsMaxLastUpdatedAcrossAllItemsAsync()
    {
        DateTimeOffset older = BaseTime;
        DateTimeOffset newer = BaseTime.AddMinutes(5);
        this._seedContext.PullRequests.Add(CreatePr("owner/pr-repo", id: 1, lastUpdated: older));
        this._seedContext.Issues.Add(CreateIssue("owner/issue-repo", id: 2, lastUpdated: newer));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        PrioritiesResponse response = await this.GetResponseAsync(owners: [], repos: []);

        Assert.Equal(expected: newer, actual: response.AsOf);
    }

    [Fact]
    public async Task LagSeconds_IsNowMinusAsOfInWholeSecondsAsync()
    {
        DateTimeOffset lastUpdated = BaseTime;
        this._timeProvider.SetUtcNow(BaseTime.AddSeconds(47));
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1, lastUpdated: lastUpdated));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        PrioritiesResponse response = await this.GetResponseAsync(owners: [], repos: []);

        Assert.Equal(expected: 47L, actual: response.LagSeconds);
    }

    [Fact]
    public async Task AsOf_IsNowWhenNoItemsExistAsync()
    {
        PrioritiesResponse response = await this.GetResponseAsync(owners: [], repos: []);

        Assert.Equal(expected: BaseTime, actual: response.AsOf);
        Assert.Equal(expected: 0L, actual: response.LagSeconds);
    }

    [Fact]
    public async Task PullRequestFromInactiveRepoIsExcludedAsync()
    {
        this._seedContext.Repos.Add(CreateRepo("owner/repo", isActive: false));
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Empty(result);
    }

    [Fact]
    public async Task PullRequestFromActiveRepoIsIncludedAsync()
    {
        this._seedContext.Repos.Add(CreateRepo("owner/repo", isActive: true));
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Single(result);
    }

    [Fact]
    public async Task IssueFromInactiveRepoIsExcludedAsync()
    {
        this._seedContext.Repos.Add(CreateRepo("owner/repo", isActive: false));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Empty(result);
    }

    [Fact]
    public async Task IssueFromActiveRepoIsIncludedAsync()
    {
        this._seedContext.Repos.Add(CreateRepo("owner/repo", isActive: true));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Single(result);
    }

    [Fact]
    public async Task PullRequestFromUntrackedRepoIsIncludedAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Single(result);
    }

    [Fact]
    public async Task IssueFromUntrackedRepoIsIncludedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Single(result);
    }

    [Fact]
    public async Task Issues_InRepoWithoutOpenPr_AreIncludedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 1, actual: single.Id);
    }

    [Fact]
    public async Task Issues_InDifferentRepoFromOpenPr_AreIncludedAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/pr-repo", id: 1));
        this._seedContext.Issues.Add(CreateIssue("owner/issue-repo", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Contains(result, w => string.Equals(w.ItemType, "Issue", StringComparison.Ordinal) && w.Id == 2);
    }

    [Fact]
    public async Task Issues_EachRepoContributesAtMostOneAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo-a", id: 1, priority: WorkPriority.HIGH));
        this._seedContext.Issues.Add(CreateIssue("owner/repo-a", id: 2, priority: WorkPriority.LOW));
        this._seedContext.Issues.Add(CreateIssue("owner/repo-b", id: 3, priority: WorkPriority.MEDIUM));
        this._seedContext.Issues.Add(CreateIssue("owner/repo-b", id: 4, priority: WorkPriority.LOW));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Contains(
            result,
            w => string.Equals(w.Repository, "owner/repo-a", StringComparison.Ordinal) && w.Id == 1
        );
        Assert.Contains(
            result,
            w => string.Equals(w.Repository, "owner/repo-b", StringComparison.Ordinal) && w.Id == 3
        );
    }

    [Fact]
    public async Task Issues_MaxIssuesCap_LimitsReturnedIssuesAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo-a", id: 1));
        this._seedContext.Issues.Add(CreateIssue("owner/repo-b", id: 2));
        this._seedContext.Issues.Add(CreateIssue("owner/repo-c", id: 3));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: [], maxIssues: 2);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.All(result, w => Assert.Equal(expected: "Issue", actual: w.ItemType));
    }

    [Fact]
    public async Task Issues_MaxIssuesCap_DoesNotAffectPullRequestsAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/pr-repo", id: 10));
        this._seedContext.Issues.Add(CreateIssue("owner/repo-a", id: 1));
        this._seedContext.Issues.Add(CreateIssue("owner/repo-b", id: 2));
        this._seedContext.Issues.Add(CreateIssue("owner/repo-c", id: 3));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: [], maxIssues: 1);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Contains(result, w => string.Equals(w.ItemType, "PullRequest", StringComparison.Ordinal) && w.Id == 10);
        Assert.Single(result, w => string.Equals(w.ItemType, "Issue", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Issues_WhenTotalUnderCap_AllAreReturnedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo-a", id: 1));
        this._seedContext.Issues.Add(CreateIssue("owner/repo-b", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: [], maxIssues: 10);

        Assert.Equal(expected: 2, actual: result.Count);
    }

    [Fact]
    public async Task Issues_MaxIssuesCap_TakesFirstNAfterOrderingAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo-a", id: 1, priority: WorkPriority.LOW));
        this._seedContext.Issues.Add(CreateIssue("owner/repo-b", id: 2, priority: WorkPriority.URGENT));
        this._seedContext.Issues.Add(CreateIssue("owner/repo-c", id: 3, priority: WorkPriority.HIGH));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: ["owner/repo-b", "owner/repo-c", "owner/repo-a"],
            maxIssues: 2
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Contains(result, w => w.Id == 2);
        Assert.Contains(result, w => w.Id == 3);
        Assert.DoesNotContain(result, w => w.Id == 1);
    }

    [Fact]
    public async Task CloseStaleItemsForRepoAsync_ClosesOpenPrNotInActiveListAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1));
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        await this._repository.CloseStaleItemsForRepoAsync(
            repository: "owner/repo",
            activePullRequestNumbers: [2],
            activeIssueNumbers: [],
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 2, actual: single.Id);
    }

    [Fact]
    public async Task CloseStaleItemsForRepoAsync_DoesNotClosePrInActiveListAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        await this._repository.CloseStaleItemsForRepoAsync(
            repository: "owner/repo",
            activePullRequestNumbers: [1],
            activeIssueNumbers: [],
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 1, actual: single.Id);
    }

    [Fact]
    public async Task CloseStaleItemsForRepoAsync_ClosesOpenIssueNotInActiveListAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 10));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 20));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        await this._repository.CloseStaleItemsForRepoAsync(
            repository: "owner/repo",
            activePullRequestNumbers: [],
            activeIssueNumbers: [20],
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 20, actual: single.Id);
    }

    [Fact]
    public async Task CloseStaleItemsForRepoAsync_DoesNotCloseAlreadyClosedPrAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1, status: "Closed"));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        await this._repository.CloseStaleItemsForRepoAsync(
            repository: "owner/repo",
            activePullRequestNumbers: [],
            activeIssueNumbers: [],
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Empty(result);
    }

    [Fact]
    public async Task CloseStaleItemsForRepoAsync_DoesNotAffectOtherReposAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1));
        this._seedContext.PullRequests.Add(CreatePr("owner/other-repo", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        await this._repository.CloseStaleItemsForRepoAsync(
            repository: "owner/repo",
            activePullRequestNumbers: [],
            activeIssueNumbers: [],
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: "owner/other-repo", actual: single.Repository);
        Assert.Equal(expected: 2, actual: single.Id);
    }

    [Fact]
    public async Task CloseStaleItemsForRepoAsync_SetsWhenClosedTimestampOnStalePrAsync()
    {
        DateTimeOffset closeTime = BaseTime.AddHours(1);
        this._timeProvider.SetUtcNow(closeTime);
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        await this._repository.CloseStaleItemsForRepoAsync(
            repository: "owner/repo",
            activePullRequestNumbers: [],
            activeIssueNumbers: [],
            cancellationToken: this.CancellationToken()
        );

        PullRequestEntity? entity = await this
            ._seedContext.PullRequests.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Repository == "owner/repo" && e.Id == 1, this.CancellationToken());
        Assert.NotNull(entity);

        Assert.Equal(expected: closeTime, actual: entity.WhenClosed);
    }

    [Fact]
    public async Task CloseStaleItemsForRepoAsync_SetsWhenClosedTimestampOnStaleIssueAsync()
    {
        DateTimeOffset closeTime = BaseTime.AddHours(1);
        this._timeProvider.SetUtcNow(closeTime);
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 10));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        await this._repository.CloseStaleItemsForRepoAsync(
            repository: "owner/repo",
            activePullRequestNumbers: [],
            activeIssueNumbers: [],
            cancellationToken: this.CancellationToken()
        );

        IssueEntity? entity = await this
            ._seedContext.Issues.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Repository == "owner/repo" && e.Id == 10, this.CancellationToken());
        Assert.NotNull(entity);

        Assert.Equal(expected: closeTime, actual: entity.WhenClosed);
    }

    [Fact]
    public async Task CloseStaleItemsForRepoAsync_WithEmptyActiveListsAndNoItems_DoesNothingAsync()
    {
        await this._repository.CloseStaleItemsForRepoAsync(
            repository: "owner/repo",
            activePullRequestNumbers: [],
            activeIssueNumbers: [],
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Empty(result);
    }

    private async Task<IReadOnlyList<WorkItem>> GetItemsAsync(
        IReadOnlyList<string> owners,
        IReadOnlyList<string> repos,
        TimeSpan stuckDependabotTimeout = default,
        int maxIssues = int.MaxValue
    )
    {
        PrioritiesResponse response = await this.GetResponseAsync(
            owners: owners,
            repos: repos,
            stuckDependabotTimeout: stuckDependabotTimeout,
            maxIssues: maxIssues
        );

        return response.Priorities;
    }

    private Task<PrioritiesResponse> GetResponseAsync(
        IReadOnlyList<string> owners,
        IReadOnlyList<string> repos,
        in TimeSpan stuckDependabotTimeout = default,
        int maxIssues = int.MaxValue
    )
    {
        return this._repository.GetPrioritisedWorkItemsAsync(
            owners: owners,
            repos: repos,
            stuckDependabotTimeout: stuckDependabotTimeout,
            maxIssues: maxIssues,
            cancellationToken: this.CancellationToken()
        );
    }

    private static PullRequestEntity CreatePr(
        string repository,
        int id,
        string status = "Open",
        DateTimeOffset? firstSeen = null,
        DateTimeOffset? lastUpdated = null,
        DateTimeOffset? whenClosed = null,
        int commentCount = 0,
        string? reviewDecision = null,
        int failedCheckCount = 0,
        string? failedCheckNames = null,
        WorkPriority priority = WorkPriority.UNKNOWN,
        string? author = null
    )
    {
        return new PullRequestEntity
        {
            Repository = repository,
            Id = id,
            Status = status,
            FirstSeen = firstSeen ?? BaseTime,
            LastUpdated = lastUpdated ?? BaseTime,
            WhenClosed = whenClosed,
            CommentCount = commentCount,
            ReviewDecision = reviewDecision,
            FailedCheckCount = failedCheckCount,
            FailedCheckNames = failedCheckNames,
            Priority = priority,
            Author = author,
        };
    }

    private static IssueEntity CreateIssue(
        string repository,
        int id,
        WorkPriority priority = WorkPriority.UNKNOWN,
        bool isOnHold = false,
        int? linkedPrNumber = null,
        DateTimeOffset? firstSeen = null,
        DateTimeOffset? lastUpdated = null
    )
    {
        return new IssueEntity
        {
            Repository = repository,
            Id = id,
            Status = "Open",
            Priority = priority,
            IsOnHold = isOnHold,
            LinkedPrNumber = linkedPrNumber,
            FirstSeen = firstSeen ?? BaseTime,
            LastUpdated = lastUpdated ?? BaseTime,
        };
    }

    private static RepoEntity CreateRepo(string repository, bool isActive)
    {
        return new RepoEntity
        {
            Repository = repository,
            IsActive = isActive,
            LastUpdated = BaseTime,
        };
    }

    private sealed class TestDbContextFactory : IDbContextFactory<DispatcherDbContext>
    {
        private readonly DbContextOptions<DispatcherDbContext> _options;

        public TestDbContextFactory(DbContextOptions<DispatcherDbContext> options)
        {
            this._options = options;
        }

        public DispatcherDbContext CreateDbContext()
        {
            return new DispatcherDbContext(this._options);
        }

        public Task<DispatcherDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DispatcherDbContext(this._options));
        }
    }
}
