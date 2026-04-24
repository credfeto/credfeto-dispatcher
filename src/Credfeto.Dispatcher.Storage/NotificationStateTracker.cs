using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Entities;
using Credfeto.Dispatcher.Storage.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Credfeto.Dispatcher.Storage;

public sealed class NotificationStateTracker : INotificationStateTracker
{
    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;

    public NotificationStateTracker(IDbContextFactory<DispatcherDbContext> dbContextFactory, ICurrentTimeSource currentTimeSource)
    {
        this._dbContextFactory = dbContextFactory;
        this._currentTimeSource = currentTimeSource;
    }

    public async Task<bool> PullRequestExistsAsync(GitHubNotification notification, int number, CancellationToken cancellationToken)
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await context.PullRequests.FindAsync(keyValues: [notification.Repository.FullName, number], cancellationToken: cancellationToken) is not null;
    }

    [SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "Structurally identical but operating on different entity types (PullRequestEntity vs IssueEntity).")]
    public async Task<bool> ShouldSkipPullRequestAsync(GitHubNotification notification, PullRequestDetails details, CancellationToken cancellationToken)
    {
        // Skip if closed and already marked as closed
        if (IsClosedStatus(details.Status))
        {
            return true;
        }

        // Skip if state hasn't changed
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        string repository = notification.Repository.FullName;
        PullRequestEntity? existing = await context.PullRequests.FindAsync(keyValues: [repository, details.Number], cancellationToken: cancellationToken);

        if (existing is null)
        {
            // New PR - don't skip
            return false;
        }

        string newState = NotificationStateSerializer.SerializePullRequest(details);
        bool stateChanged = !string.Equals(a: existing.State, b: newState, comparisonType: StringComparison.Ordinal);

        return !stateChanged;
    }

    [SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "Structurally identical but operating on different entity types (PullRequestEntity vs IssueEntity).")]
    public async Task UpdatePullRequestStateAsync(GitHubNotification notification, PullRequestDetails details, CancellationToken cancellationToken)
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        string repository = notification.Repository.FullName;
        PullRequestEntity? existing = await context.PullRequests.FindAsync(keyValues: [repository, details.Number], cancellationToken: cancellationToken);
        DateTimeOffset now = this._currentTimeSource.UtcNow();
        string newState = NotificationStateSerializer.SerializePullRequest(details);

        if (existing is null)
        {
            PullRequestEntity entity = CreatePullRequestEntity(repository: repository, id: details.Number, status: details.Status, priority: details.Priority, onHold: details.OnHold, state: newState, now: now);
            context.PullRequests.Add(entity);
        }
        else
        {
            existing.State = newState;
            existing.Priority = details.Priority;
            existing.OnHold = details.OnHold;
            UpdateEntityStatus(entity: existing, status: details.Status, now: now);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IssueExistsAsync(GitHubNotification notification, int number, CancellationToken cancellationToken)
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Issues.FindAsync(keyValues: [notification.Repository.FullName, number], cancellationToken: cancellationToken) is not null;
    }

    [SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "Structurally identical but operating on different entity types (PullRequestEntity vs IssueEntity).")]
    public async Task<bool> ShouldSkipIssueAsync(GitHubNotification notification, IssueDetails details, CancellationToken cancellationToken)
    {
        // Skip if closed and already marked as closed
        if (IsClosedStatus(details.Status))
        {
            return true;
        }

        // Skip if state hasn't changed
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        string repository = notification.Repository.FullName;
        IssueEntity? existing = await context.Issues.FindAsync(keyValues: [repository, details.Number], cancellationToken: cancellationToken);

        if (existing is null)
        {
            // New issue - don't skip
            return false;
        }

        string newState = NotificationStateSerializer.SerializeIssue(details);
        bool stateChanged = !string.Equals(a: existing.State, b: newState, comparisonType: StringComparison.Ordinal);

        return !stateChanged;
    }

    [SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "Structurally identical but operating on different entity types (PullRequestEntity vs IssueEntity).")]
    public async Task UpdateIssueStateAsync(GitHubNotification notification, IssueDetails details, CancellationToken cancellationToken)
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        string repository = notification.Repository.FullName;
        IssueEntity? existing = await context.Issues.FindAsync(keyValues: [repository, details.Number], cancellationToken: cancellationToken);
        DateTimeOffset now = this._currentTimeSource.UtcNow();
        string newState = NotificationStateSerializer.SerializeIssue(details);

        if (existing is null)
        {
            IssueEntity entity = CreateIssueEntity(repository: repository, id: details.Number, status: details.Status, priority: details.Priority, onHold: details.OnHold, state: newState, now: now);
            context.Issues.Add(entity);
        }
        else
        {
            existing.State = newState;
            existing.Priority = details.Priority;
            existing.OnHold = details.OnHold;
            UpdateEntityStatus(entity: existing, status: details.Status, now: now);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsClosedStatus(WorkItemStatus status)
    {
        return status == WorkItemStatus.Closed;
    }

    private static PullRequestEntity CreatePullRequestEntity(string repository, int id, WorkItemStatus status, WorkItemPriority priority, bool onHold, string state, in DateTimeOffset now)
    {
        return new PullRequestEntity
        {
            Repository = repository,
            Id = id,
            Status = status,
            Priority = priority,
            OnHold = onHold,
            State = state,
            FirstSeen = now,
            LastUpdated = now,
            WhenClosed = IsClosedStatus(status) ? now : null,
        };
    }

    private static IssueEntity CreateIssueEntity(string repository, int id, WorkItemStatus status, WorkItemPriority priority, bool onHold, string state, in DateTimeOffset now)
    {
        return new IssueEntity
        {
            Repository = repository,
            Id = id,
            Status = status,
            Priority = priority,
            OnHold = onHold,
            State = state,
            FirstSeen = now,
            LastUpdated = now,
            WhenClosed = IsClosedStatus(status) ? now : null,
        };
    }

    private static void UpdateEntityStatus(INotificationEntity entity, WorkItemStatus status, in DateTimeOffset now)
    {
        entity.Status = status;
        entity.LastUpdated = now;

        if (IsClosedStatus(status))
        {
            entity.WhenClosed ??= now;
        }
        else
        {
            entity.WhenClosed = null;
        }
    }
}
