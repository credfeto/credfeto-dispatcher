using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace Credfeto.Dispatcher.Storage;

public sealed class PendingNotificationStore : IPendingNotificationStore
{
    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;

    public PendingNotificationStore(
        IDbContextFactory<DispatcherDbContext> dbContextFactory,
        ICurrentTimeSource currentTimeSource
    )
    {
        this._dbContextFactory = dbContextFactory;
        this._currentTimeSource = currentTimeSource;
    }

    public async Task EnqueueAsync(
        GitHubNotification notification,
        DateTimeOffset dispatchAfter,
        CancellationToken cancellationToken
    )
    {
        Uri subjectUrl = notification.Subject.Url;
        DateTimeOffset now = this._currentTimeSource.UtcNow();

        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(
            cancellationToken
        );
        NotificationQueueEntity? existing = await context.NotificationQueue.FindAsync(
            keyValues: [subjectUrl],
            cancellationToken: cancellationToken
        );

        if (existing is null)
        {
            context.NotificationQueue.Add(
                CreateEntity(
                    notification: notification,
                    subjectUrl: subjectUrl,
                    queuedAt: now,
                    dispatchAfter: dispatchAfter
                )
            );
        }
        else
        {
            UpdateEntity(
                entity: existing,
                notification: notification,
                queuedAt: now,
                dispatchAfter: dispatchAfter
            );
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveIfPresentAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    )
    {
        Uri subjectUrl = notification.Subject.Url;

        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(
            cancellationToken
        );
        NotificationQueueEntity? existing = await context.NotificationQueue.FindAsync(
            keyValues: [subjectUrl],
            cancellationToken: cancellationToken
        );

        if (existing is null)
        {
            return;
        }

        context.NotificationQueue.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GitHubNotification>> GetReadyItemsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(
            cancellationToken
        );

        List<NotificationQueueEntity> entities = await context
            .NotificationQueue.Where(e => e.DispatchAfter <= now)
            .OrderBy(e => e.QueuedAt)
            .ToListAsync(cancellationToken);

        return entities.ConvertAll(ToNotification);
    }

    public Task RemoveAsync(GitHubNotification notification, CancellationToken cancellationToken)
    {
        return this.RemoveIfPresentAsync(
            notification: notification,
            cancellationToken: cancellationToken
        );
    }

    private static NotificationQueueEntity CreateEntity(
        GitHubNotification notification,
        Uri subjectUrl,
        in DateTimeOffset queuedAt,
        in DateTimeOffset dispatchAfter
    )
    {
        return new NotificationQueueEntity
        {
            SubjectUrl = subjectUrl,
            NotificationId = notification.Id,
            Repository = notification.Repository.FullName,
            RepositoryUrl = notification.Repository.Url,
            SubjectType = notification.Subject.Type,
            SubjectTitle = notification.Subject.Title,
            Reason = notification.Reason,
            UpdatedAt = notification.UpdatedAt,
            QueuedAt = queuedAt,
            DispatchAfter = dispatchAfter,
        };
    }

    private static void UpdateEntity(
        NotificationQueueEntity entity,
        GitHubNotification notification,
        in DateTimeOffset queuedAt,
        in DateTimeOffset dispatchAfter
    )
    {
        entity.NotificationId = notification.Id;
        entity.Reason = notification.Reason;
        entity.SubjectTitle = notification.Subject.Title;
        entity.UpdatedAt = notification.UpdatedAt;
        entity.QueuedAt = queuedAt;
        entity.DispatchAfter = dispatchAfter;
    }

    private static GitHubNotification ToNotification(NotificationQueueEntity entity)
    {
        return new GitHubNotification(
            Id: entity.NotificationId,
            Reason: entity.Reason,
            Subject: new NotificationSubject(
                Title: entity.SubjectTitle,
                Url: entity.SubjectUrl,
                Type: entity.SubjectType
            ),
            Repository: new NotificationRepository(
                FullName: entity.Repository,
                Url: entity.RepositoryUrl
            ),
            UpdatedAt: entity.UpdatedAt,
            Unread: true
        );
    }
}
