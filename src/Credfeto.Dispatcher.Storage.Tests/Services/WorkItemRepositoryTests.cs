using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Entities;
using FunFair.Test.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class WorkItemRepositoryTests : TestBase, IAsyncLifetime
{
    private static readonly DateTimeOffset BaseTime = new(
        year: 2024,
        month: 1,
        day: 1,
        hour: 0,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    private readonly SqliteConnection _connection;
    private readonly IWorkItemRepository _repository;
    private readonly DbContextOptions<DispatcherDbContext> _options;

    public WorkItemRepositoryTests()
    {
        this._connection = new SqliteConnection("DataSource=:memory:");
        this._connection.Open();

        this._options = new DbContextOptionsBuilder<DispatcherDbContext>()
            .UseSqlite(this._connection)
            .Options;

        using (DispatcherDbContext ctx = new(this._options))
        {
            ctx.Database.Migrate();
        }

        this._repository = new WorkItemRepository(new TestDbContextFactory(this._options));
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return this._connection.DisposeAsync();
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncReturnsEmptyWhenNothingInDatabaseAsync()
    {
        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncExcludesClosedPullRequestsAsync()
    {
        await this.AddPullRequestAsync(
            repository: "owner/repo",
            id: 1,
            status: "Closed",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncExcludesOnHoldPullRequestsAsync()
    {
        await this.AddPullRequestAsync(
            repository: "owner/repo",
            id: 1,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime,
            isOnHold: true
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncExcludesClosedIssuesAsync()
    {
        await this.AddIssueAsync(
            repository: "owner/repo",
            id: 1,
            status: "Closed",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncExcludesOnHoldIssuesAsync()
    {
        await this.AddIssueAsync(
            repository: "owner/repo",
            id: 1,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime,
            isOnHold: true
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncExcludesIssuesWithLinkedPrAsync()
    {
        await this.AddIssueAsync(
            repository: "owner/repo",
            id: 1,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime,
            hasLinkedPr: true
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncReturnsOpenPullRequestAsync()
    {
        await this.AddPullRequestAsync(
            repository: "owner/repo",
            id: 42,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        WorkItem item = Assert.Single(result);
        Assert.Equal(expected: "owner/repo", actual: item.Repository);
        Assert.Equal(expected: 42, actual: item.Id);
        Assert.Equal(expected: "PullRequest", actual: item.ItemType);
        Assert.Equal(expected: WorkPriority.Medium, actual: item.Priority);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncReturnsOpenIssueWithoutLinkedPrAsync()
    {
        await this.AddIssueAsync(
            repository: "owner/repo",
            id: 7,
            status: "Open",
            priority: WorkPriority.Low,
            firstSeen: BaseTime
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        WorkItem item = Assert.Single(result);
        Assert.Equal(expected: "owner/repo", actual: item.Repository);
        Assert.Equal(expected: 7, actual: item.Id);
        Assert.Equal(expected: "Issue", actual: item.ItemType);
        Assert.Equal(expected: WorkPriority.Low, actual: item.Priority);
        Assert.Null(item.IsUpToDate);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncReturnsPullRequestBeforeIssueAsync()
    {
        await this.AddIssueAsync(
            repository: "owner/repo",
            id: 1,
            status: "Open",
            priority: WorkPriority.Urgent,
            firstSeen: BaseTime
        );

        await this.AddPullRequestAsync(
            repository: "owner/repo",
            id: 2,
            status: "Open",
            priority: WorkPriority.Unknown,
            firstSeen: BaseTime.AddHours(1)
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "PullRequest", actual: result[0].ItemType);
        Assert.Equal(expected: "Issue", actual: result[1].ItemType);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncOrdersByPriorityDescendingAsync()
    {
        await this.AddIssueAsync(
            repository: "owner/repo",
            id: 1,
            status: "Open",
            priority: WorkPriority.Low,
            firstSeen: BaseTime
        );

        await this.AddIssueAsync(
            repository: "owner/repo",
            id: 2,
            status: "Open",
            priority: WorkPriority.Urgent,
            firstSeen: BaseTime.AddMinutes(1)
        );

        await this.AddIssueAsync(
            repository: "owner/repo",
            id: 3,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime.AddMinutes(2)
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 3, actual: result.Count);
        Assert.Equal(expected: WorkPriority.Urgent, actual: result[0].Priority);
        Assert.Equal(expected: WorkPriority.Medium, actual: result[1].Priority);
        Assert.Equal(expected: WorkPriority.Low, actual: result[2].Priority);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncOrdersByFirstSeenAscendingAsync()
    {
        await this.AddIssueAsync(
            repository: "owner/repo",
            id: 1,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime.AddHours(2)
        );

        await this.AddIssueAsync(
            repository: "owner/repo",
            id: 2,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime
        );

        await this.AddIssueAsync(
            repository: "owner/repo",
            id: 3,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime.AddHours(1)
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 3, actual: result.Count);
        Assert.Equal(expected: 2, actual: result[0].Id);
        Assert.Equal(expected: 3, actual: result[1].Id);
        Assert.Equal(expected: 1, actual: result[2].Id);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncOrdersByOwnerPriorityAsync()
    {
        await this.AddIssueAsync(
            repository: "other/repo",
            id: 1,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime
        );

        await this.AddIssueAsync(
            repository: "preferred/repo",
            id: 2,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: ["preferred"],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "preferred/repo", actual: result[0].Repository);
        Assert.Equal(expected: "other/repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncOrdersByRepoPriorityAsync()
    {
        await this.AddIssueAsync(
            repository: "owner/other-repo",
            id: 1,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime
        );

        await this.AddIssueAsync(
            repository: "owner/preferred-repo",
            id: 2,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: ["owner/preferred-repo"],
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "owner/preferred-repo", actual: result[0].Repository);
        Assert.Equal(expected: "owner/other-repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncUnknownOwnerAppearsAfterKnownOwnersAsync()
    {
        await this.AddIssueAsync(
            repository: "z-unknown/repo",
            id: 1,
            status: "Open",
            priority: WorkPriority.Urgent,
            firstSeen: BaseTime
        );

        await this.AddIssueAsync(
            repository: "a-known/repo",
            id: 2,
            status: "Open",
            priority: WorkPriority.Unknown,
            firstSeen: BaseTime
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: ["a-known"],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "a-known/repo", actual: result[0].Repository);
        Assert.Equal(expected: "z-unknown/repo", actual: result[1].Repository);
    }

    [Fact]
    public async Task GetPrioritisedWorkItemsAsyncPullRequestExposeIsUpToDateAsync()
    {
        await this.AddPullRequestAsync(
            repository: "owner/repo",
            id: 1,
            status: "Open",
            priority: WorkPriority.Medium,
            firstSeen: BaseTime,
            isUpToDate: true
        );

        IReadOnlyList<WorkItem> result = await this._repository.GetPrioritisedWorkItemsAsync(
            owners: [],
            repos: [],
            cancellationToken: this.CancellationToken()
        );

        WorkItem item = Assert.Single(result);
        Assert.True(item.IsUpToDate);
    }

    private async Task AddPullRequestAsync(
        string repository,
        int id,
        string status,
        WorkPriority priority,
        DateTimeOffset firstSeen,
        bool isOnHold = false,
        bool? isUpToDate = null
    )
    {
        await using DispatcherDbContext ctx = new(this._options);
        ctx.PullRequests.Add(
            new PullRequestEntity
            {
                Repository = repository,
                Id = id,
                Status = status,
                Priority = priority,
                FirstSeen = firstSeen,
                LastUpdated = firstSeen,
                IsOnHold = isOnHold,
                IsUpToDate = isUpToDate,
            }
        );
        await ctx.SaveChangesAsync(this.CancellationToken());
    }

    private async Task AddIssueAsync(
        string repository,
        int id,
        string status,
        WorkPriority priority,
        DateTimeOffset firstSeen,
        bool isOnHold = false,
        bool hasLinkedPr = false
    )
    {
        await using DispatcherDbContext ctx = new(this._options);
        ctx.Issues.Add(
            new IssueEntity
            {
                Repository = repository,
                Id = id,
                Status = status,
                Priority = priority,
                FirstSeen = firstSeen,
                LastUpdated = firstSeen,
                IsOnHold = isOnHold,
                HasLinkedPr = hasLinkedPr,
            }
        );
        await ctx.SaveChangesAsync(this.CancellationToken());
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

        public Task<DispatcherDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(new DispatcherDbContext(this._options));
        }
    }
}
