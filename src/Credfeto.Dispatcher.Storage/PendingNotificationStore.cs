using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Database;
using Credfeto.Date.Interfaces;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage;

public sealed class PendingNotificationStore : IPendingNotificationStore
{
    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly IDatabase _database;

    public PendingNotificationStore(IDatabase database, ICurrentTimeSource currentTimeSource)
    {
        this._database = database;
        this._currentTimeSource = currentTimeSource;
    }

    public async Task EnqueueAsync(
        GitHubNotification notification,
        DateTimeOffset dispatchAfter,
        CancellationToken cancellationToken
    )
    {
        DateTimeOffset now = this._currentTimeSource.UtcNow();

        await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.NotificationQueue_UpsertAsync(
                    connection: c,
                    subjectUrl: notification.Subject.Url.AbsoluteUri,
                    notificationId: notification.Id,
                    repository: notification.Repository.FullName,
                    repositoryUrl: notification.Repository.Url.AbsoluteUri,
                    subjectType: notification.Subject.Type,
                    subjectTitle: notification.Subject.Title,
                    reason: notification.Reason,
                    updatedAt: notification.UpdatedAt,
                    queuedAt: now,
                    dispatchAfter: dispatchAfter,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }

    public async Task RemoveIfPresentAsync(GitHubNotification notification, CancellationToken cancellationToken)
    {
        await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.NotificationQueue_DeleteAsync(
                    connection: c,
                    subjectUrl: notification.Subject.Url.AbsoluteUri,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }

    public async Task<IReadOnlyList<GitHubNotification>> GetReadyItemsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<NotificationQueueRow> rows = await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.NotificationQueue_GetReadyAsync(connection: c, now: now, cancellationToken: ct),
            cancellationToken: cancellationToken
        );

        return [.. rows.Select(ToNotification)];
    }

    public Task RemoveAsync(GitHubNotification notification, CancellationToken cancellationToken)
    {
        return this.RemoveIfPresentAsync(notification: notification, cancellationToken: cancellationToken);
    }

    private static GitHubNotification ToNotification(NotificationQueueRow row)
    {
        return new GitHubNotification(
            Id: row.NotificationId,
            Reason: row.Reason,
            Subject: new NotificationSubject(
                Title: row.SubjectTitle,
                Url: new Uri(row.SubjectUrl),
                Type: row.SubjectType
            ),
            Repository: new NotificationRepository(FullName: row.Repository, Url: new Uri(row.RepositoryUrl)),
            UpdatedAt: row.UpdatedAt,
            Unread: true
        );
    }
}
