using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace Credfeto.Dispatcher.Storage;

public sealed class NotificationStateTracker : INotificationStateTracker
{
    private const string ClosedStatus = "Closed";

    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public NotificationStateTracker(
        IDbContextFactory<DispatcherDbContext> dbContextFactory,
        TimeProvider timeProvider
    )
    {
        this._dbContextFactory = dbContextFactory;
        this._timeProvider = timeProvider;
    }

    public Task<bool> ShouldSkipPullRequestAsync(
        string repository,
        int pullRequestNumber,
        string currentStatus,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(IsClosedStatus(currentStatus));
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical but operating on different entity types (PullRequestEntity vs IssueEntity)."
    )]
    public async Task UpdatePullRequestStateAsync(
        string repository,
        int pullRequestNumber,
        string status,
        WorkPriority priority,
        bool isOnHold,
        bool? isUpToDate,
        CancellationToken cancellationToken
    )
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(
            cancellationToken
        );
        PullRequestEntity? existing = await context.PullRequests.FindAsync(
            keyValues: [repository, pullRequestNumber],
            cancellationToken: cancellationToken
        );
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        if (existing is null)
        {
            context.PullRequests.Add(
                CreatePullRequestEntity(
                    repository: repository,
                    id: pullRequestNumber,
                    status: status,
                    priority: priority,
                    isOnHold: isOnHold,
                    isUpToDate: isUpToDate,
                    now: now
                )
            );
        }
        else
        {
            UpdatePullRequestEntity(
                entity: existing,
                status: status,
                priority: priority,
                isOnHold: isOnHold,
                isUpToDate: isUpToDate,
                now: now
            );
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ShouldSkipIssueAsync(
        string repository,
        int issueNumber,
        string currentStatus,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(IsClosedStatus(currentStatus));
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical but operating on different entity types (PullRequestEntity vs IssueEntity)."
    )]
    public async Task UpdateIssueStateAsync(
        string repository,
        int issueNumber,
        string status,
        WorkPriority priority,
        bool isOnHold,
        bool hasLinkedPr,
        CancellationToken cancellationToken
    )
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(
            cancellationToken
        );
        IssueEntity? existing = await context.Issues.FindAsync(
            keyValues: [repository, issueNumber],
            cancellationToken: cancellationToken
        );
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        if (existing is null)
        {
            context.Issues.Add(
                CreateIssueEntity(
                    repository: repository,
                    id: issueNumber,
                    status: status,
                    priority: priority,
                    isOnHold: isOnHold,
                    hasLinkedPr: hasLinkedPr,
                    now: now
                )
            );
        }
        else
        {
            UpdateIssueEntity(
                entity: existing,
                status: status,
                priority: priority,
                isOnHold: isOnHold,
                hasLinkedPr: hasLinkedPr,
                now: now
            );
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsClosedStatus(string status)
    {
        return string.Equals(
            a: status,
            b: ClosedStatus,
            comparisonType: StringComparison.OrdinalIgnoreCase
        );
    }

    private static PullRequestEntity CreatePullRequestEntity(
        string repository,
        int id,
        string status,
        WorkPriority priority,
        bool isOnHold,
        bool? isUpToDate,
        in DateTimeOffset now
    )
    {
        return new PullRequestEntity
        {
            Repository = repository,
            Id = id,
            Status = status,
            Priority = priority,
            IsOnHold = isOnHold,
            IsUpToDate = isUpToDate,
            FirstSeen = now,
            LastUpdated = now,
            WhenClosed = IsClosedStatus(status) ? now : null,
        };
    }

    private static IssueEntity CreateIssueEntity(
        string repository,
        int id,
        string status,
        WorkPriority priority,
        bool isOnHold,
        bool hasLinkedPr,
        in DateTimeOffset now
    )
    {
        return new IssueEntity
        {
            Repository = repository,
            Id = id,
            Status = status,
            Priority = priority,
            IsOnHold = isOnHold,
            HasLinkedPr = hasLinkedPr,
            FirstSeen = now,
            LastUpdated = now,
            WhenClosed = IsClosedStatus(status) ? now : null,
        };
    }

    private static void UpdatePullRequestEntity(
        PullRequestEntity entity,
        string status,
        WorkPriority priority,
        bool isOnHold,
        bool? isUpToDate,
        in DateTimeOffset now
    )
    {
        entity.Status = status;
        entity.Priority = priority;
        entity.IsOnHold = isOnHold;
        entity.LastUpdated = now;

        if (isUpToDate.HasValue)
        {
            entity.IsUpToDate = isUpToDate.Value;
        }

        if (IsClosedStatus(status))
        {
            entity.WhenClosed ??= now;
        }
        else
        {
            entity.WhenClosed = null;
        }
    }

    private static void UpdateIssueEntity(
        IssueEntity entity,
        string status,
        WorkPriority priority,
        bool isOnHold,
        bool hasLinkedPr,
        in DateTimeOffset now
    )
    {
        entity.Status = status;
        entity.Priority = priority;
        entity.IsOnHold = isOnHold;
        entity.HasLinkedPr = hasLinkedPr;
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
