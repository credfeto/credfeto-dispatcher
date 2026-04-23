using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace Credfeto.Dispatcher.Storage;

public sealed class NotificationStateTracker : INotificationStateTracker
{
    private const string ClosedStatus = "Closed";

    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;

    public NotificationStateTracker(IDbContextFactory<DispatcherDbContext> dbContextFactory, ICurrentTimeSource currentTimeSource)
    {
        this._dbContextFactory = dbContextFactory;
        this._currentTimeSource = currentTimeSource;
    }

    public Task<bool> ShouldSkipPullRequestAsync(GitHubNotification notification, PullRequestDetails details, CancellationToken cancellationToken)
    {
        return Task.FromResult(IsClosedStatus(details.Status));
    }

    [SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "Structurally identical but operating on different entity types (PullRequestEntity vs IssueEntity).")]
    public async Task UpdatePullRequestStateAsync(GitHubNotification notification, PullRequestDetails details, CancellationToken cancellationToken)
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        string repository = notification.Repository.FullName;
        PullRequestEntity? existing = await context.PullRequests.FindAsync(keyValues: [repository, details.Number], cancellationToken: cancellationToken);
        DateTimeOffset now = this._currentTimeSource.UtcNow();

        if (existing is null)
        {
            context.PullRequests.Add(CreatePullRequestEntity(repository: repository, id: details.Number, status: details.Status, now: now));
        }
        else
        {
            UpdateEntityStatus(entity: existing, status: details.Status, now: now);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ShouldSkipIssueAsync(GitHubNotification notification, IssueDetails details, CancellationToken cancellationToken)
    {
        return Task.FromResult(IsClosedStatus(details.Status));
    }

    [SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "Structurally identical but operating on different entity types (PullRequestEntity vs IssueEntity).")]
    public async Task UpdateIssueStateAsync(GitHubNotification notification, IssueDetails details, CancellationToken cancellationToken)
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        string repository = notification.Repository.FullName;
        IssueEntity? existing = await context.Issues.FindAsync(keyValues: [repository, details.Number], cancellationToken: cancellationToken);
        DateTimeOffset now = this._currentTimeSource.UtcNow();

        if (existing is null)
        {
            context.Issues.Add(CreateIssueEntity(repository: repository, id: details.Number, status: details.Status, now: now));
        }
        else
        {
            UpdateEntityStatus(entity: existing, status: details.Status, now: now);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsClosedStatus(string status)
    {
        return string.Equals(a: status, b: ClosedStatus, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static PullRequestEntity CreatePullRequestEntity(string repository, int id, string status, in DateTimeOffset now)
    {
        return new PullRequestEntity
        {
            Repository = repository,
            Id = id,
            Status = status,
            FirstSeen = now,
            LastUpdated = now,
            WhenClosed = IsClosedStatus(status) ? now : null,
        };
    }

    private static IssueEntity CreateIssueEntity(string repository, int id, string status, in DateTimeOffset now)
    {
        return new IssueEntity
        {
            Repository = repository,
            Id = id,
            Status = status,
            FirstSeen = now,
            LastUpdated = now,
            WhenClosed = IsClosedStatus(status) ? now : null,
        };
    }

    private static void UpdateEntityStatus(INotificationEntity entity, string status, in DateTimeOffset now)
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
