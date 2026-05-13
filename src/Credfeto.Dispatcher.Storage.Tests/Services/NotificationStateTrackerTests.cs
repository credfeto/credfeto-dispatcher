using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using FunFair.Test.Common;
using FunFair.Test.Common.Mocks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class NotificationStateTrackerTests : LoggingFolderCleanupTestBase
{
    private const string TestRepository = "test-owner/test-repo";
    private const int TestPullRequestNumber = 42;
    private const int TestIssueNumber = 7;
    private const string OpenStatus = "Open";
    private const string ClosedStatus = "Closed";

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

        this._tracker = new NotificationStateTracker(new TestDbContextFactory(options), MockDateTimeSources.Past);
    }

    private static GitHubNotification BuildNotification()
    {
        return new GitHubNotification(
            Id: "test-1",
            Reason: "subscribed",
            Subject: new NotificationSubject(
                Title: "Test",
                Url: new Uri("https://api.github.com/repos/test-owner/test-repo/pulls/42"),
                Type: "PullRequest"
            ),
            Repository: new NotificationRepository(
                FullName: TestRepository,
                Url: new Uri("https://github.com/test-owner/test-repo")
            ),
            UpdatedAt: MockDateTimeSources.Past.GetUtcNow(),
            Unread: true
        );
    }

    private static PullRequestDetails BuildPullRequestDetails(string status)
    {
        return new PullRequestDetails(
            Number: TestPullRequestNumber,
            Title: "Test PR",
            Status: status,
            HtmlUrl: new Uri("https://github.com/test-owner/test-repo/pull/42"),
            Assignees: [],
            Labels: [],
            Body: null,
            Comments: [],
            Reviews: [],
            Runs: [],
            LinkedItems: [],
            Repository: new ItemRepository(
                Owner: "test-owner",
                Name: "test-repo",
                Url: new Uri("https://github.com/test-owner/test-repo")
            ),
            LastNotification: new LastNotification(Id: "test-1", Timestamp: MockDateTimeSources.Past.GetUtcNow()),
            Author: null
        );
    }

    private static IssueDetails BuildIssueDetails(string status)
    {
        return new IssueDetails(
            Number: TestIssueNumber,
            Title: "Test Issue",
            Status: status,
            HtmlUrl: new Uri("https://github.com/test-owner/test-repo/issues/7"),
            Assignees: [],
            Labels: [],
            LinkedPullRequestUrl: null,
            Repository: new ItemRepository(
                Owner: "test-owner",
                Name: "test-repo",
                Url: new Uri("https://github.com/test-owner/test-repo")
            ),
            LastNotification: new LastNotification(Id: "test-1", Timestamp: MockDateTimeSources.Past.GetUtcNow())
        );
    }

    [Fact]
    public async Task ShouldSkipPullRequestReturnsFalseForOpenStatusAsync()
    {
        bool result = await this._tracker.ShouldSkipAsync(
            notification: BuildNotification(),
            details: BuildPullRequestDetails(OpenStatus),
            cancellationToken: this.CancellationToken()
        );

        Assert.False(result, "Expected ShouldSkipPullRequest to return false for non-closed status");
    }

    [Fact]
    public async Task ShouldSkipPullRequestReturnsTrueForClosedStatusAsync()
    {
        bool result = await this._tracker.ShouldSkipAsync(
            notification: BuildNotification(),
            details: BuildPullRequestDetails(ClosedStatus),
            cancellationToken: this.CancellationToken()
        );

        Assert.True(result, "Expected ShouldSkipPullRequest to return true for closed status");
    }

    [Fact]
    public async Task UpdatePullRequestStateCreatesNewRecordAsync()
    {
        await this._tracker.UpdateStateAsync(
            notification: BuildNotification(),
            details: BuildPullRequestDetails(OpenStatus),
            priority: WorkPriority.Unknown,
            isOnHold: false,
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task UpdatePullRequestStateUpdatesExistingRecordAsync()
    {
        GitHubNotification notification = BuildNotification();

        await this._tracker.UpdateStateAsync(
            notification: notification,
            details: BuildPullRequestDetails(OpenStatus),
            priority: WorkPriority.Unknown,
            isOnHold: false,
            cancellationToken: this.CancellationToken()
        );

        await this._tracker.UpdateStateAsync(
            notification: notification,
            details: BuildPullRequestDetails(ClosedStatus),
            priority: WorkPriority.Unknown,
            isOnHold: false,
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task ShouldSkipIssueReturnsFalseForOpenStatusAsync()
    {
        bool result = await this._tracker.ShouldSkipAsync(
            notification: BuildNotification(),
            details: BuildIssueDetails(OpenStatus),
            cancellationToken: this.CancellationToken()
        );

        Assert.False(result, "Expected ShouldSkipIssue to return false for non-closed status");
    }

    [Fact]
    public async Task ShouldSkipIssueReturnsTrueForClosedStatusAsync()
    {
        bool result = await this._tracker.ShouldSkipAsync(
            notification: BuildNotification(),
            details: BuildIssueDetails(ClosedStatus),
            cancellationToken: this.CancellationToken()
        );

        Assert.True(result, "Expected ShouldSkipIssue to return true for closed status");
    }

    [Fact]
    public async Task UpdateIssueStateCreatesNewRecordAsync()
    {
        await this._tracker.UpdateStateAsync(
            notification: BuildNotification(),
            details: BuildIssueDetails(OpenStatus),
            priority: WorkPriority.Unknown,
            isOnHold: false,
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task UpdateIssueStateUpdatesExistingRecordAsync()
    {
        GitHubNotification notification = BuildNotification();

        await this._tracker.UpdateStateAsync(
            notification: notification,
            details: BuildIssueDetails(OpenStatus),
            priority: WorkPriority.Unknown,
            isOnHold: false,
            cancellationToken: this.CancellationToken()
        );

        await this._tracker.UpdateStateAsync(
            notification: notification,
            details: BuildIssueDetails(ClosedStatus),
            priority: WorkPriority.Unknown,
            isOnHold: false,
            cancellationToken: this.CancellationToken()
        );
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
