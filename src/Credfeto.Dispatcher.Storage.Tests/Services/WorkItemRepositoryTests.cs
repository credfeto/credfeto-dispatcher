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
            ctx.Database.Migrate();
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
    public async Task PullRequestsAppearBeforeIssuesForSameOwnerAndRepoAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 10));
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 20, firstSeen: BaseTime.AddSeconds(1)));
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
    public async Task IssuesAreOrderedByPriorityDescendingAsync()
    {
        await this.SeedIssuePrioritiesAsync();

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 5, actual: result.Count);
        Assert.Equal(expected: WorkPriority.Urgent, actual: result[0].Priority);
        Assert.Equal(expected: WorkPriority.High, actual: result[1].Priority);
        Assert.Equal(expected: WorkPriority.Medium, actual: result[2].Priority);
        Assert.Equal(expected: WorkPriority.Low, actual: result[3].Priority);
        Assert.Equal(expected: WorkPriority.Unknown, actual: result[4].Priority);
    }

    [Fact]
    public async Task UntaggedIssuesAppearAfterLowPriorityIssuesAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1, priority: WorkPriority.Unknown));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 2, priority: WorkPriority.Low));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: WorkPriority.Low, actual: result[0].Priority);
        Assert.Equal(expected: WorkPriority.Unknown, actual: result[1].Priority);
    }

    [Fact]
    public async Task WithEqualPriority_OlderItemsAppearFirstAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1, firstSeen: BaseTime.AddDays(1)));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 2, firstSeen: BaseTime));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: 2, actual: result[0].Id);
        Assert.Equal(expected: 1, actual: result[1].Id);
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
    public async Task IssuesWithLinkedPrsAreExcludedAsync()
    {
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 1, linkedPrNumber: 42));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 2));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: 2, actual: single.Id);
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
        Assert.Equal(expected: ReviewDecisionState.ChangesRequested, actual: single.ReviewDecision);
    }

    [Fact]
    public async Task PullRequest_ReviewDecisionIsNotReviewedWhenNotSetAsync()
    {
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1));
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(owners: [], repos: []);

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: ReviewDecisionState.NotReviewed, actual: single.ReviewDecision);
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
        Assert.Equal(expected: ReviewDecisionState.NotApplicable, actual: single.ReviewDecision);
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
            CreatePr("owner/repo", id: 1, firstSeen: BaseTime, priority: WorkPriority.Low, author: "dependabot[bot]")
        );
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        this._timeProvider.SetUtcNow(BaseTime.AddHours(3));

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: [],
            stuckDependabotTimeout: TimeSpan.FromHours(3)
        );

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: WorkPriority.Security, actual: single.Priority);
    }

    [Fact]
    public async Task StuckDependabotPullRequestBelowThresholdKeepsOriginalPriorityAsync()
    {
        this._seedContext.PullRequests.Add(
            CreatePr("owner/repo", id: 1, firstSeen: BaseTime, priority: WorkPriority.Low, author: "dependabot[bot]")
        );
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        this._timeProvider.SetUtcNow(BaseTime.AddHours(2).AddMinutes(59));

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: [],
            stuckDependabotTimeout: TimeSpan.FromHours(3)
        );

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: WorkPriority.Low, actual: single.Priority);
    }

    [Fact]
    public async Task StuckPullRequestFromNonDependabotAuthorKeepsOriginalPriorityAsync()
    {
        this._seedContext.PullRequests.Add(
            CreatePr("owner/repo", id: 1, firstSeen: BaseTime, priority: WorkPriority.Low, author: "someone-else")
        );
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        this._timeProvider.SetUtcNow(BaseTime.AddDays(7));

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: [],
            stuckDependabotTimeout: TimeSpan.FromHours(3)
        );

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: WorkPriority.Low, actual: single.Priority);
    }

    [Fact]
    public async Task StuckDependabotRuleIsDisabledWhenTimeoutIsZeroAsync()
    {
        this._seedContext.PullRequests.Add(
            CreatePr("owner/repo", id: 1, firstSeen: BaseTime, priority: WorkPriority.Low, author: "dependabot[bot]")
        );
        await this._seedContext.SaveChangesAsync(this.CancellationToken());

        this._timeProvider.SetUtcNow(BaseTime.AddDays(7));

        IReadOnlyList<WorkItem> result = await this.GetItemsAsync(
            owners: [],
            repos: [],
            stuckDependabotTimeout: TimeSpan.Zero
        );

        WorkItem single = Assert.Single(result);
        Assert.Equal(expected: WorkPriority.Low, actual: single.Priority);
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
        this._seedContext.PullRequests.Add(CreatePr("owner/repo", id: 1, lastUpdated: older));
        this._seedContext.Issues.Add(CreateIssue("owner/repo", id: 2, lastUpdated: newer));
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

    private async Task SeedIssuePrioritiesAsync()
    {
        await this._seedContext.Issues.AddRangeAsync(
            CreateIssue("owner/repo", id: 1, priority: WorkPriority.Low),
            CreateIssue("owner/repo", id: 2, priority: WorkPriority.Urgent),
            CreateIssue("owner/repo", id: 3, priority: WorkPriority.Medium),
            CreateIssue("owner/repo", id: 4, priority: WorkPriority.High),
            CreateIssue("owner/repo", id: 5, priority: WorkPriority.Unknown)
        );
        await this._seedContext.SaveChangesAsync(this.CancellationToken());
    }

    private async Task<IReadOnlyList<WorkItem>> GetItemsAsync(
        IReadOnlyList<string> owners,
        IReadOnlyList<string> repos,
        TimeSpan stuckDependabotTimeout = default
    )
    {
        PrioritiesResponse response = await this.GetResponseAsync(
            owners: owners,
            repos: repos,
            stuckDependabotTimeout: stuckDependabotTimeout
        );

        return response.Priorities;
    }

    private Task<PrioritiesResponse> GetResponseAsync(
        IReadOnlyList<string> owners,
        IReadOnlyList<string> repos,
        in TimeSpan stuckDependabotTimeout = default
    )
    {
        return this._repository.GetPrioritisedWorkItemsAsync(
            owners: owners,
            repos: repos,
            stuckDependabotTimeout: stuckDependabotTimeout,
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
        WorkPriority priority = WorkPriority.Unknown,
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
        WorkPriority priority = WorkPriority.Unknown,
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
