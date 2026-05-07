using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using FunFair.Test.Common;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class PendingNotificationStoreTests : LoggingFolderCleanupTestBase
{
    private static readonly DateTimeOffset TestNow = new(
        year: 2025,
        month: 1,
        day: 1,
        hour: 12,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    private static readonly GitHubNotification TestNotification = new(
        Id: "notification-1",
        Reason: "subscribed",
        Subject: new NotificationSubject(
            Title: "Fix: resolve issue",
            Url: new Uri("https://github.com/test-owner/test-repo/issues/1"),
            Type: "Issue"
        ),
        Repository: new NotificationRepository(
            FullName: "test-owner/test-repo",
            Url: new Uri("https://github.com/test-owner/test-repo")
        ),
        UpdatedAt: TestNow,
        Unread: true
    );

    private static readonly GitHubNotification AnotherNotification = new(
        Id: "notification-2",
        Reason: "mention",
        Subject: new NotificationSubject(
            Title: "Feature: add new API",
            Url: new Uri("https://github.com/test-owner/test-repo/pull/2"),
            Type: "PullRequest"
        ),
        Repository: new NotificationRepository(
            FullName: "test-owner/test-repo",
            Url: new Uri("https://github.com/test-owner/test-repo")
        ),
        UpdatedAt: TestNow,
        Unread: true
    );

    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly IPendingNotificationStore _store;

    public PendingNotificationStoreTests(ITestOutputHelper output)
        : base(output)
    {
        string dbPath = Path.Combine(this.TempFolder, "test.db");
        DbContextOptions<DispatcherDbContext> options =
            new DbContextOptionsBuilder<DispatcherDbContext>()
                .UseSqlite($"DataSource={dbPath}")
                .Options;

        using (DispatcherDbContext ctx = new(options))
        {
            ctx.Database.Migrate();
        }

        this._currentTimeSource = GetSubstitute<ICurrentTimeSource>();
        this._currentTimeSource.UtcNow().Returns(TestNow);

        this._store = new PendingNotificationStore(
            new TestDbContextFactory(options),
            this._currentTimeSource
        );
    }

    [Fact]
    public async Task EnqueueAsyncAddsNotificationThatAppearsInReadyItemsAsync()
    {
        await this._store.EnqueueAsync(
            notification: TestNotification,
            dispatchAfter: TestNow.AddMinutes(-1),
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<GitHubNotification> items = await this._store.GetReadyItemsAsync(
            now: TestNow,
            cancellationToken: this.CancellationToken()
        );

        Assert.Single(items);
        Assert.Equal(expected: TestNotification.Id, actual: items[0].Id);
    }

    [Fact]
    public async Task EnqueueAsyncDoesNotReturnItemsBeforeDispatchTimeAsync()
    {
        DateTimeOffset futureDispatch = TestNow.AddHours(1);
        await this._store.EnqueueAsync(
            notification: TestNotification,
            dispatchAfter: futureDispatch,
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<GitHubNotification> items = await this._store.GetReadyItemsAsync(
            now: TestNow,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(items);
    }

    [Fact]
    public async Task EnqueueAsyncUpdatesExistingNotificationForSameSubjectUrlAsync()
    {
        GitHubNotification updatedNotification = TestNotification with
        {
            Id = "notification-updated",
        };

        await this._store.EnqueueAsync(
            notification: TestNotification,
            dispatchAfter: TestNow.AddMinutes(-1),
            cancellationToken: this.CancellationToken()
        );
        await this._store.EnqueueAsync(
            notification: updatedNotification,
            dispatchAfter: TestNow.AddMinutes(-1),
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<GitHubNotification> items = await this._store.GetReadyItemsAsync(
            now: TestNow,
            cancellationToken: this.CancellationToken()
        );

        Assert.Single(items);
        Assert.Equal(expected: updatedNotification.Id, actual: items[0].Id);
    }

    [Fact]
    public Task RemoveIfPresentAsyncDoesNotThrowWhenNotificationAbsentAsync()
    {
        return this._store.RemoveIfPresentAsync(
            notification: TestNotification,
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task RemoveIfPresentAsyncRemovesEnqueuedNotificationAsync()
    {
        await this._store.EnqueueAsync(
            notification: TestNotification,
            dispatchAfter: TestNow.AddMinutes(-1),
            cancellationToken: this.CancellationToken()
        );
        await this._store.RemoveIfPresentAsync(
            notification: TestNotification,
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<GitHubNotification> items = await this._store.GetReadyItemsAsync(
            now: TestNow,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetReadyItemsAsyncReturnsOnlyItemsPastDispatchTimeAsync()
    {
        DateTimeOffset pastDispatch = TestNow.AddMinutes(-5);
        DateTimeOffset futureDispatch = TestNow.AddHours(1);

        await this._store.EnqueueAsync(
            notification: TestNotification,
            dispatchAfter: pastDispatch,
            cancellationToken: this.CancellationToken()
        );
        await this._store.EnqueueAsync(
            notification: AnotherNotification,
            dispatchAfter: futureDispatch,
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<GitHubNotification> items = await this._store.GetReadyItemsAsync(
            now: TestNow,
            cancellationToken: this.CancellationToken()
        );

        Assert.Single(items);
        Assert.Equal(expected: TestNotification.Id, actual: items[0].Id);
    }

    [Fact]
    public async Task RemoveAsyncRemovesEnqueuedNotificationAsync()
    {
        await this._store.EnqueueAsync(
            notification: TestNotification,
            dispatchAfter: TestNow.AddMinutes(-1),
            cancellationToken: this.CancellationToken()
        );
        await this._store.RemoveAsync(
            notification: TestNotification,
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<GitHubNotification> items = await this._store.GetReadyItemsAsync(
            now: TestNow,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetReadyItemsAsyncReturnsEmptyListWhenQueueIsEmptyAsync()
    {
        IReadOnlyList<GitHubNotification> items = await this._store.GetReadyItemsAsync(
            now: TestNow,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(items);
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
