using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Dispatcher.Storage;
using Credfeto.Dispatcher.Storage.Entities;
using FunFair.Test.Common;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests;

public sealed class NotificationStateTrackerTests : TestBase
{
    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;
    private readonly NotificationStateTracker _tracker;

    public NotificationStateTrackerTests()
    {
        this._currentTimeSource = GetSubstitute<ICurrentTimeSource>();
        this._currentTimeSource.UtcNow.Returns(DateTimeOffset.UtcNow);

        var options = new DbContextOptionsBuilder<DispatcherDbContext>()
            .UseInMemoryDatabase(databaseName: $"test-db-{Guid.NewGuid()}")
            .Options;

        this._dbContextFactory = new FakeDbContextFactory(options);
        this._tracker = new NotificationStateTracker(dbContextFactory: this._dbContextFactory, currentTimeSource: this._currentTimeSource);
    }

    [Fact]
    public async Task ShouldSkipPullRequestAsync_ReturnsFalse_WhenCurrentStatusIsNotClosedAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            currentStatus: "Open",
            cancellationToken: token);

        Assert.False(result, "Should not skip open PRs");
    }

    [Fact]
    public async Task ShouldSkipPullRequestAsync_ReturnsFalse_WhenCurrentStatusIsClosedButNotInDatabaseAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            currentStatus: "Closed",
            cancellationToken: token);

        Assert.False(result, "Should not skip first-time closed PRs");
    }

    [Fact]
    public async Task ShouldSkipPullRequestAsync_ReturnsTrue_WhenCurrentAndStoredStatusBothClosedAndMatchAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await this._tracker.UpdatePullRequestStateAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            status: "Closed",
            cancellationToken: token);

        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            currentStatus: "Closed",
            cancellationToken: token);

        Assert.True(result, "Should skip repeated closed PRs");
    }

    [Fact]
    public async Task ShouldSkipPullRequestAsync_ReturnsFalse_WhenStoredStatusDoesNotMatchCurrentStatusAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await this._tracker.UpdatePullRequestStateAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            status: "Open",
            cancellationToken: token);

        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            currentStatus: "Closed",
            cancellationToken: token);

        Assert.False(result, "Should not skip when status changes from Open to Closed");
    }

    [Fact]
    public async Task ShouldSkipPullRequestAsync_ReturnsFalse_WhenCaseDoesNotMatchAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await this._tracker.UpdatePullRequestStateAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            status: "closed",
            cancellationToken: token);

        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            currentStatus: "CLOSED",
            cancellationToken: token);

        Assert.True(result, "Should use case-insensitive comparison for status");
    }

    [Fact]
    public async Task ShouldSkipIssueAsync_ReturnsFalse_WhenCurrentStatusIsNotClosedAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        bool result = await this._tracker.ShouldSkipIssueAsync(
            repository: "owner/repo",
            issueNumber: 1,
            currentStatus: "Open",
            cancellationToken: token);

        Assert.False(result, "Should not skip open issues");
    }

    [Fact]
    public async Task ShouldSkipIssueAsync_ReturnsFalse_WhenCurrentStatusIsClosedButNotInDatabaseAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        bool result = await this._tracker.ShouldSkipIssueAsync(
            repository: "owner/repo",
            issueNumber: 1,
            currentStatus: "Closed",
            cancellationToken: token);

        Assert.False(result, "Should not skip first-time closed issues");
    }

    [Fact]
    public async Task ShouldSkipIssueAsync_ReturnsTrue_WhenCurrentAndStoredStatusBothClosedAndMatchAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await this._tracker.UpdateIssueStateAsync(
            repository: "owner/repo",
            issueNumber: 1,
            status: "Closed",
            cancellationToken: token);

        bool result = await this._tracker.ShouldSkipIssueAsync(
            repository: "owner/repo",
            issueNumber: 1,
            currentStatus: "Closed",
            cancellationToken: token);

        Assert.True(result, "Should skip repeated closed issues");
    }

    [Fact]
    public async Task ShouldSkipIssueAsync_ReturnsFalse_WhenStoredStatusDoesNotMatchCurrentStatusAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await this._tracker.UpdateIssueStateAsync(
            repository: "owner/repo",
            issueNumber: 1,
            status: "Open",
            cancellationToken: token);

        bool result = await this._tracker.ShouldSkipIssueAsync(
            repository: "owner/repo",
            issueNumber: 1,
            currentStatus: "Closed",
            cancellationToken: token);

        Assert.False(result, "Should not skip when status changes from Open to Closed");
    }

    [Fact]
    public async Task UpdatePullRequestStateAsync_CreatesNewRecord_WhenNotExistingAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await this._tracker.UpdatePullRequestStateAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            status: "Closed",
            cancellationToken: token);

        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            currentStatus: "Closed",
            cancellationToken: token);

        Assert.True(result, "Record should be created and subsequent skip should return true");
    }

    [Fact]
    public async Task UpdatePullRequestStateAsync_UpdatesExistingRecord_WhenChangingStatusAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await this._tracker.UpdatePullRequestStateAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            status: "Open",
            cancellationToken: token);

        await this._tracker.UpdatePullRequestStateAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            status: "Closed",
            cancellationToken: token);

        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            currentStatus: "Closed",
            cancellationToken: token);

        Assert.True(result, "Updated record should be tracked correctly");
    }

    [Fact]
    public async Task UpdatePullRequestStateAsync_ClearsWhenClosedOnReopen_WhenStatusChangesFromClosedToOpenAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await this._tracker.UpdatePullRequestStateAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            status: "Closed",
            cancellationToken: token);

        await this._tracker.UpdatePullRequestStateAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            status: "Open",
            cancellationToken: token);

        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 1,
            currentStatus: "Open",
            cancellationToken: token);

        Assert.False(result, "Reopened PRs should not be skipped");
    }

    [Fact]
    public async Task ScenarioTestPullRequestLifecycle_FirstClosed_ThenOpen_ThenClosedAgainAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        // First closure notification
        bool shouldSkip1 = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 42,
            currentStatus: "Closed",
            cancellationToken: token);
        Assert.False(shouldSkip1, "First closure should be dispatched");

        await this._tracker.UpdatePullRequestStateAsync(
            repository: "owner/repo",
            pullRequestNumber: 42,
            status: "Closed",
            cancellationToken: token);

        // Second closure notification (duplicate)
        bool shouldSkip2 = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 42,
            currentStatus: "Closed",
            cancellationToken: token);
        Assert.True(shouldSkip2, "Duplicate closure should be skipped");

        // Reopen notification
        bool shouldSkip3 = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 42,
            currentStatus: "Open",
            cancellationToken: token);
        Assert.False(shouldSkip3, "Reopen should be dispatched");

        await this._tracker.UpdatePullRequestStateAsync(
            repository: "owner/repo",
            pullRequestNumber: 42,
            status: "Open",
            cancellationToken: token);

        // Close again notification
        bool shouldSkip4 = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 42,
            currentStatus: "Closed",
            cancellationToken: token);
        Assert.False(shouldSkip4, "Second closure should be dispatched");

        await this._tracker.UpdatePullRequestStateAsync(
            repository: "owner/repo",
            pullRequestNumber: 42,
            status: "Closed",
            cancellationToken: token);

        // Third closure notification (duplicate)
        bool shouldSkip5 = await this._tracker.ShouldSkipPullRequestAsync(
            repository: "owner/repo",
            pullRequestNumber: 42,
            currentStatus: "Closed",
            cancellationToken: token);
        Assert.True(shouldSkip5, "Duplicate second closure should be skipped");
    }

    private sealed class FakeDbContextFactory : IDbContextFactory<DispatcherDbContext>
    {
        private readonly DbContextOptions<DispatcherDbContext> _options;

        public FakeDbContextFactory(DbContextOptions<DispatcherDbContext> options)
        {
            this._options = options;
        }

        public DispatcherDbContext CreateDbContext()
        {
            return new DispatcherDbContext(this._options);
        }
    }
}