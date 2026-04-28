using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Dispatcher.GitHub.Interfaces;
using FunFair.Test.Common;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class NotificationStateTrackerTests : LoggingFolderCleanupTestBase
{
    private static readonly DateTimeOffset TestNow = new(year: 2025, month: 1, day: 1, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

    private const string TestRepository = "test-owner/test-repo";
    private const int TestPullRequestNumber = 42;
    private const int TestIssueNumber = 7;
    private const string OpenStatus = "Open";
    private const string ClosedStatus = "Closed";

    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly INotificationStateTracker _tracker;

    public NotificationStateTrackerTests(ITestOutputHelper output)
        : base(output)
    {
        string dbPath = Path.Combine(this.TempFolder, "test.db");
        DbContextOptions<DispatcherDbContext> options = new DbContextOptionsBuilder<DispatcherDbContext>()
                                                        .UseSqlite($"DataSource={dbPath}")
                                                        .Options;

        using (DispatcherDbContext ctx = new(options))
        {
            ctx.Database.Migrate();
        }

        this._currentTimeSource = GetSubstitute<ICurrentTimeSource>();
        this._currentTimeSource.UtcNow().Returns(TestNow);

        this._tracker = new NotificationStateTracker(new TestDbContextFactory(options), this._currentTimeSource);
    }

    [Fact]
    public async Task ShouldSkipPullRequestReturnsFalseForOpenStatusAsync()
    {
        bool result = await this._tracker.ShouldSkipPullRequestAsync(repository: TestRepository,
                                                                     pullRequestNumber: TestPullRequestNumber,
                                                                     currentStatus: OpenStatus,
                                                                     cancellationToken: this.CancellationToken());

        Assert.False(result, "Expected ShouldSkipPullRequest to return false for non-closed status");
    }

    [Fact]
    public async Task ShouldSkipPullRequestReturnsTrueForClosedStatusAsync()
    {
        bool result = await this._tracker.ShouldSkipPullRequestAsync(repository: TestRepository,
                                                                     pullRequestNumber: TestPullRequestNumber,
                                                                     currentStatus: ClosedStatus,
                                                                     cancellationToken: this.CancellationToken());

        Assert.True(result, "Expected ShouldSkipPullRequest to return true for closed status");
    }

    [Fact]
    public Task UpdatePullRequestStateCreatesNewRecordAsync()
    {
        return this._tracker.UpdatePullRequestStateAsync(repository: TestRepository,
                                                         pullRequestNumber: TestPullRequestNumber,
                                                         status: OpenStatus,
                                                         cancellationToken: this.CancellationToken());
    }

    [Fact]
    public async Task UpdatePullRequestStateUpdatesExistingRecordAsync()
    {
        await this._tracker.UpdatePullRequestStateAsync(repository: TestRepository,
                                                        pullRequestNumber: TestPullRequestNumber,
                                                        status: OpenStatus,
                                                        cancellationToken: this.CancellationToken());

        await this._tracker.UpdatePullRequestStateAsync(repository: TestRepository,
                                                        pullRequestNumber: TestPullRequestNumber,
                                                        status: ClosedStatus,
                                                        cancellationToken: this.CancellationToken());
    }

    [Fact]
    public async Task ShouldSkipIssueReturnsFalseForOpenStatusAsync()
    {
        bool result = await this._tracker.ShouldSkipIssueAsync(repository: TestRepository,
                                                               issueNumber: TestIssueNumber,
                                                               currentStatus: OpenStatus,
                                                               cancellationToken: this.CancellationToken());

        Assert.False(result, "Expected ShouldSkipIssue to return false for non-closed status");
    }

    [Fact]
    public async Task ShouldSkipIssueReturnsTrueForClosedStatusAsync()
    {
        bool result = await this._tracker.ShouldSkipIssueAsync(repository: TestRepository,
                                                               issueNumber: TestIssueNumber,
                                                               currentStatus: ClosedStatus,
                                                               cancellationToken: this.CancellationToken());

        Assert.True(result, "Expected ShouldSkipIssue to return true for closed status");
    }

    [Fact]
    public Task UpdateIssueStateCreatesNewRecordAsync()
    {
        return this._tracker.UpdateIssueStateAsync(repository: TestRepository,
                                                   issueNumber: TestIssueNumber,
                                                   status: OpenStatus,
                                                   cancellationToken: this.CancellationToken());
    }

    [Fact]
    public async Task UpdateIssueStateUpdatesExistingRecordAsync()
    {
        await this._tracker.UpdateIssueStateAsync(repository: TestRepository,
                                                  issueNumber: TestIssueNumber,
                                                  status: OpenStatus,
                                                  cancellationToken: this.CancellationToken());

        await this._tracker.UpdateIssueStateAsync(repository: TestRepository,
                                                  issueNumber: TestIssueNumber,
                                                  status: ClosedStatus,
                                                  cancellationToken: this.CancellationToken());
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
