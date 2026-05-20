using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database.Rows;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class PendingNotificationStoreTests : TestBase
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

    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly TestDatabaseStub _database;
    private readonly IPendingNotificationStore _store;

    public PendingNotificationStoreTests()
    {
        this._database = new TestDatabaseStub();
        this._database.SetReturn<IReadOnlyList<NotificationQueueRow>>([]);

        this._currentTimeSource = GetSubstitute<ICurrentTimeSource>();
        this._currentTimeSource.UtcNow().Returns(BaseTime);
        this._store = new PendingNotificationStore(this._database, this._currentTimeSource);
    }

    private static GitHubNotification CreateNotification(string repo = "owner/repo")
    {
        return new GitHubNotification(
            Id: "notif-1",
            Reason: "review_requested",
            Subject: new NotificationSubject(
                Title: "Test PR",
                Url: new Uri("https://api.github.com/repos/owner/repo/pulls/1"),
                Type: "PullRequest"
            ),
            Repository: new NotificationRepository(
                FullName: repo,
                Url: new Uri("https://api.github.com/repos/owner/repo")
            ),
            UpdatedAt: BaseTime,
            Unread: true
        );
    }

    [Fact]
    public async Task EnqueueAsync_CallsDatabaseAsync()
    {
        await this._store.EnqueueAsync(
            notification: CreateNotification(),
            dispatchAfter: BaseTime.AddMinutes(5),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 1, actual: this._database.VoidExecuteCallCount);
    }

    [Fact]
    public async Task RemoveIfPresentAsync_CallsDatabaseAsync()
    {
        await this._store.RemoveIfPresentAsync(
            notification: CreateNotification(),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 1, actual: this._database.VoidExecuteCallCount);
    }

    [Fact]
    public async Task GetReadyItemsAsync_ReturnsEmptyWhenDatabaseReturnsEmptyAsync()
    {
        IReadOnlyList<GitHubNotification> result = await this._store.GetReadyItemsAsync(
            now: BaseTime,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetReadyItemsAsync_MapsRowsToNotificationsAsync()
    {
        NotificationQueueRow row = new(
            SubjectUrl: "https://api.github.com/repos/owner/repo/pulls/1",
            NotificationId: "notif-1",
            Repository: "owner/repo",
            RepositoryUrl: "https://api.github.com/repos/owner/repo",
            SubjectType: "PullRequest",
            SubjectTitle: "Test PR",
            Reason: "review_requested",
            UpdatedAt: BaseTime,
            QueuedAt: BaseTime,
            DispatchAfter: BaseTime
        );

        this._database.SetReturn<IReadOnlyList<NotificationQueueRow>>([row]);

        IReadOnlyList<GitHubNotification> result = await this._store.GetReadyItemsAsync(
            now: BaseTime,
            cancellationToken: this.CancellationToken()
        );

        Assert.Single(result);
        Assert.Equal(expected: "notif-1", actual: result[0].Id);
        Assert.Equal(expected: "owner/repo", actual: result[0].Repository.FullName);
    }
}
