using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Dispatcher.Storage;
using Credfeto.Dispatcher.Storage.Entities;
using FunFair.Test.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class NotificationStateTrackerTests : TestBase, IAsyncLifetime
{
    private const string Repository = "owner/repo";
    private const int PullRequestNumber = 42;
    private const int IssueNumber = 10;

    private readonly NotificationStateTracker _tracker;
    private readonly SqliteConnection _connection;
    private readonly ICurrentTimeSource _currentTimeSource;

    public NotificationStateTrackerTests()
    {
        this._currentTimeSource = GetSubstitute<ICurrentTimeSource>();
        this._currentTimeSource.UtcNow.Returns(DateTimeOffset.UtcNow);

        this._connection = new SqliteConnection("DataSource=:memory:");
        this._connection.Open();

        DbContextOptions<DispatcherDbContext> options = new DbContextOptionsBuilder<DispatcherDbContext>()
                                                        .UseSqlite(this._connection)
                                                        .Options;

        using (DispatcherDbContext ctx = new(options))
        {
            ctx.Database.Migrate();
        }

        this._tracker = new NotificationStateTracker(new TestDbContextFactory(options), this._currentTimeSource);
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
    public async Task ShouldSkipPullRequestAsync_ReturnsFalse_WhenCurrentStatusIsNotClosedAsync()
    {
        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: Repository,
            pullRequestNumber: PullRequestNumber,
            currentStatus: "Open",
            cancellationToken: this.CancellationToken());

        Assert.False(result);
    }

    [Fact]
    public async Task ShouldSkipPullRequestAsync_ReturnsFalse_WhenFirstTimeClosedAsync()
    {
        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: Repository,
            pullRequestNumber: PullRequestNumber,
            currentStatus: "Closed",
            cancellationToken: this.CancellationToken());

        Assert.False(result, "First-time closed PR should not be skipped");
    }

    [Fact]
    public async Task ShouldSkipPullRequestAsync_ReturnsTrue_WhenDuplicateClosedAsync()
    {
        await this._tracker.UpdatePullRequestStateAsync(
            repository: Repository,
            pullRequestNumber: PullRequestNumber,
            status: "Closed",
            cancellationToken: this.CancellationToken());

        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: Repository,
            pullRequestNumber: PullRequestNumber,
            currentStatus: "Closed",
            cancellationToken: this.CancellationToken());

        Assert.True(result, "Duplicate closed PR should be skipped");
    }

    [Fact]
    public async Task ShouldSkipPullRequestAsync_ReturnsFalse_WhenStatusChangesAsync()
    {
        await this._tracker.UpdatePullRequestStateAsync(
            repository: Repository,
            pullRequestNumber: PullRequestNumber,
            status: "Open",
            cancellationToken: this.CancellationToken());

        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: Repository,
            pullRequestNumber: PullRequestNumber,
            currentStatus: "Closed",
            cancellationToken: this.CancellationToken());

        Assert.False(result, "Status change should not be skipped");
    }

    [Fact]
    public async Task ShouldSkipPullRequestAsync_IgnoresCaseWhenComparingAsync()
    {
        await this._tracker.UpdatePullRequestStateAsync(
            repository: Repository,
            pullRequestNumber: PullRequestNumber,
            status: "closed",
            cancellationToken: this.CancellationToken());

        bool result = await this._tracker.ShouldSkipPullRequestAsync(
            repository: Repository,
            pullRequestNumber: PullRequestNumber,
            currentStatus: "CLOSED",
            cancellationToken: this.CancellationToken());

        Assert.True(result, "Case-insensitive comparison should work");
    }

    [Fact]
    public async Task ShouldSkipIssueAsync_ReturnsFalse_WhenCurrentStatusIsNotClosedAsync()
    {
        bool result = await this._tracker.ShouldSkipIssueAsync(
            repository: Repository,
            issueNumber: IssueNumber,
            currentStatus: "Open",
            cancellationToken: this.CancellationToken());

        Assert.False(result);
    }

    [Fact]
    public async Task ShouldSkipIssueAsync_ReturnsFalse_WhenFirstTimeClosedAsync()
    {
        bool result = await this._tracker.ShouldSkipIssueAsync(
            repository: Repository,
            issueNumber: IssueNumber,
            currentStatus: "Closed",
            cancellationToken: this.CancellationToken());

        Assert.False(result, "First-time closed issue should not be skipped");
    }

    [Fact]
    public async Task ShouldSkipIssueAsync_ReturnsTrue_WhenDuplicateClosedAsync()
    {
        await this._tracker.UpdateIssueStateAsync(
            repository: Repository,
            issueNumber: IssueNumber,
            status: "Closed",
            cancellationToken: this.CancellationToken());

        bool result = await this._tracker.ShouldSkipIssueAsync(
            repository: Repository,
            issueNumber: IssueNumber,
            currentStatus: "Closed",
            cancellationToken: this.CancellationToken());

        Assert.True(result, "Duplicate closed issue should be skipped");
    }

    [Fact]
    public async Task ShouldSkipIssueAsync_ReturnsFalse_WhenStatusChangesAsync()
    {
        await this._tracker.UpdateIssueStateAsync(
            repository: Repository,
            issueNumber: IssueNumber,
            status: "Open",
            cancellationToken: this.CancellationToken());

        bool result = await this._tracker.ShouldSkipIssueAsync(
            repository: Repository,
            issueNumber: IssueNumber,
            currentStatus: "Closed",
            cancellationToken: this.CancellationToken());

        Assert.False(result, "Status change should not be skipped");
    }

    [Fact]
    public async Task FullPullRequestLifecycle_ClosedThenOpenThenClosedAgainAsync()
    {
        bool shouldSkip1 = await this._tracker.ShouldSkipPullRequestAsync(Repository, PullRequestNumber, "Closed", this.CancellationToken());
        Assert.False(shouldSkip1, "First closure should be dispatched");

        await this._tracker.UpdatePullRequestStateAsync(Repository, PullRequestNumber, "Closed", this.CancellationToken());

        bool shouldSkip2 = await this._tracker.ShouldSkipPullRequestAsync(Repository, PullRequestNumber, "Closed", this.CancellationToken());
        Assert.True(shouldSkip2, "Duplicate closure should be skipped");

        bool shouldSkip3 = await this._tracker.ShouldSkipPullRequestAsync(Repository, PullRequestNumber, "Open", this.CancellationToken());
        Assert.False(shouldSkip3, "Reopen should be dispatched");

        await this._tracker.UpdatePullRequestStateAsync(Repository, PullRequestNumber, "Open", this.CancellationToken());

        bool shouldSkip4 = await this._tracker.ShouldSkipPullRequestAsync(Repository, PullRequestNumber, "Closed", this.CancellationToken());
        Assert.False(shouldSkip4, "Second closure should be dispatched");

        await this._tracker.UpdatePullRequestStateAsync(Repository, PullRequestNumber, "Closed", this.CancellationToken());

        bool shouldSkip5 = await this._tracker.ShouldSkipPullRequestAsync(Repository, PullRequestNumber, "Closed", this.CancellationToken());
        Assert.True(shouldSkip5, "Duplicate second closure should be skipped");
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
    }
}
