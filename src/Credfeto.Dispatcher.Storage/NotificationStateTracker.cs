using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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

    private static readonly ImmutableHashSet<string> FailedConclusions = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "failure",
        "error",
        "timed_out",
        "action_required"
    );

    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public NotificationStateTracker(IDbContextFactory<DispatcherDbContext> dbContextFactory, TimeProvider timeProvider)
    {
        this._dbContextFactory = dbContextFactory;
        this._timeProvider = timeProvider;
    }

    public ValueTask<bool> ShouldSkipAsync(
        GitHubNotification notification,
        PullRequestDetails details,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(IsClosedStatus(details.Status));
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical but operating on different entity types (PullRequestEntity vs IssueEntity)."
    )]
    public async ValueTask UpdateStateAsync(
        GitHubNotification notification,
        PullRequestDetails details,
        WorkPriority priority,
        bool isOnHold,
        CancellationToken cancellationToken
    )
    {
        string repository = notification.Repository.FullName;
        int pullRequestNumber = details.Number;
        string status = details.Status;
        int commentCount = details.Comments.Count;
        string? reviewDecision = ComputeReviewDecision(details.Reviews);
        int failedCheckCount = CountFailedChecks(details.Runs);
        string? failedCheckNames = BuildFailedCheckNames(details.Runs);
        string? failedCheckSha = BuildFailedCheckSha(details.Runs);
        string? author = details.Author;

        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
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
                    commentCount: commentCount,
                    reviewDecision: reviewDecision,
                    failedCheckCount: failedCheckCount,
                    failedCheckNames: failedCheckNames,
                    failedCheckSha: failedCheckSha,
                    author: author,
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
                commentCount: commentCount,
                reviewDecision: reviewDecision,
                failedCheckCount: failedCheckCount,
                failedCheckNames: failedCheckNames,
                failedCheckSha: failedCheckSha,
                author: author,
                now: now
            );
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public ValueTask<bool> ShouldSkipAsync(
        GitHubNotification notification,
        IssueDetails details,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(IsClosedStatus(details.Status));
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical but operating on different entity types (PullRequestEntity vs IssueEntity)."
    )]
    public async ValueTask UpdateStateAsync(
        GitHubNotification notification,
        IssueDetails details,
        WorkPriority priority,
        bool isOnHold,
        bool hasAssignee,
        bool isAiWork,
        CancellationToken cancellationToken
    )
    {
        string repository = notification.Repository.FullName;
        int issueNumber = details.Number;
        string status = details.Status;
        int? linkedPrNumber = ExtractPrNumber(details.LinkedPullRequestUrl);

        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
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
                    hasAssignee: hasAssignee,
                    isAiWork: isAiWork,
                    linkedPrNumber: linkedPrNumber,
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
                hasAssignee: hasAssignee,
                isAiWork: isAiWork,
                linkedPrNumber: linkedPrNumber,
                now: now
            );
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsClosedStatus(string status)
    {
        return string.Equals(a: status, b: ClosedStatus, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static int? ExtractPrNumber(Uri? linkedPullRequestUrl)
    {
        if (linkedPullRequestUrl is null)
        {
            return null;
        }

        string[] segments = linkedPullRequestUrl.AbsolutePath.TrimEnd('/').Split('/');

        return int.TryParse(
            segments[^1],
            style: System.Globalization.NumberStyles.Integer,
            provider: System.Globalization.CultureInfo.InvariantCulture,
            out int number
        )
            ? number
            : null;
    }

    private static PullRequestEntity CreatePullRequestEntity(
        string repository,
        int id,
        string status,
        WorkPriority priority,
        bool isOnHold,
        int commentCount,
        string? reviewDecision,
        int failedCheckCount,
        string? failedCheckNames,
        string? failedCheckSha,
        string? author,
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
            CommentCount = commentCount,
            ReviewDecision = reviewDecision,
            FailedCheckCount = failedCheckCount,
            FailedCheckNames = failedCheckNames,
            FailedCheckSha = failedCheckSha,
            Author = author,
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
        bool hasAssignee,
        bool isAiWork,
        int? linkedPrNumber,
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
            HasAssignee = hasAssignee,
            IsAiWork = isAiWork,
            LinkedPrNumber = linkedPrNumber,
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
        int commentCount,
        string? reviewDecision,
        int failedCheckCount,
        string? failedCheckNames,
        string? failedCheckSha,
        string? author,
        in DateTimeOffset now
    )
    {
        entity.Status = status;
        entity.Priority = priority;
        entity.IsOnHold = isOnHold;
        entity.CommentCount = commentCount;
        entity.ReviewDecision = reviewDecision;
        entity.FailedCheckCount = failedCheckCount;
        entity.FailedCheckNames = failedCheckNames;
        entity.FailedCheckSha = failedCheckSha;
        entity.Author = author ?? entity.Author;
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

    private static string? ComputeReviewDecision(IReadOnlyList<PullRequestReview> reviews)
    {
        if (reviews.Any(r => string.Equals(r.State, "CHANGES_REQUESTED", StringComparison.OrdinalIgnoreCase)))
        {
            return "ChangesRequested";
        }

        if (reviews.Any(r => string.Equals(r.State, "APPROVED", StringComparison.OrdinalIgnoreCase)))
        {
            return "Approved";
        }

        return null;
    }

    private static int CountFailedChecks(IReadOnlyList<PullRequestRun> runs)
    {
        return runs.Count(r => r.Conclusion is not null && FailedConclusions.Contains(r.Conclusion));
    }

    private static string? BuildFailedCheckNames(IReadOnlyList<PullRequestRun> runs)
    {
        string[] failed =
        [
            .. runs.Where(r => r.Conclusion is not null && FailedConclusions.Contains(r.Conclusion))
                .Select(r => r.Name),
        ];

        return failed.Length > 0 ? string.Join(separator: ',', failed) : null;
    }

    private static string? BuildFailedCheckSha(IReadOnlyList<PullRequestRun> runs)
    {
        string[] shas =
        [
            .. runs.Where(r => r.Conclusion is not null && FailedConclusions.Contains(r.Conclusion))
                .Select(r => r.HeadSha)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];

        return shas.Length > 0 ? shas[0] : null;
    }

    private static void UpdateIssueEntity(
        IssueEntity entity,
        string status,
        WorkPriority priority,
        bool isOnHold,
        bool hasAssignee,
        bool isAiWork,
        int? linkedPrNumber,
        in DateTimeOffset now
    )
    {
        entity.Status = status;
        entity.Priority = priority;
        entity.IsOnHold = isOnHold;
        entity.HasAssignee = hasAssignee;
        entity.IsAiWork = isAiWork;
        entity.LinkedPrNumber = linkedPrNumber;
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
