using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace Credfeto.Dispatcher.Storage;

public sealed class WorkItemRepository : IWorkItemRepository
{
    private const string PullRequestType = "PullRequest";
    private const string IssueType = "Issue";
    private const string ClosedStatus = "Closed";
    private const string DependabotLogin = "dependabot[bot]";

    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public WorkItemRepository(IDbContextFactory<DispatcherDbContext> dbContextFactory, TimeProvider timeProvider)
    {
        this._dbContextFactory = dbContextFactory;
        this._timeProvider = timeProvider;
    }

    public async Task<PrioritiesResponse> GetPrioritisedWorkItemsAsync(
        IReadOnlyList<string> owners,
        IReadOnlyList<string> repos,
        TimeSpan stuckDependabotTimeout,
        int maxIssues,
        CancellationToken cancellationToken
    )
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<PullRequestEntity> prEntities = await context
            .PullRequests.Where(e =>
                e.Status != ClosedStatus
                && !e.IsOnHold
                && !context.Repos.Any(r => r.Repository == e.Repository && !r.IsActive)
            )
            .ToListAsync(cancellationToken);

        List<WorkItem> issues = await BuildIssuesAsync(
            context: context,
            owners: owners,
            repos: repos,
            maxIssues: maxIssues,
            cancellationToken: cancellationToken
        );

        DateTimeOffset now = this._timeProvider.GetUtcNow();

        List<WorkItem> pullRequests =
        [
            .. prEntities.Select(e => MapPullRequest(entity: e, now: now, stuckDependabotTimeout)),
        ];

        List<WorkItem> combined = Deduplicate([.. pullRequests, .. issues]);

        IReadOnlyList<WorkItem> ordered =
        [
            .. combined
                .OrderBy(w => FindIndex(owners, GetOwner(w.Repository)))
                .ThenBy(w => GetOwner(w.Repository), comparer: StringComparer.OrdinalIgnoreCase)
                .ThenBy(w => IsPullRequest(w) ? 0 : 1)
                .ThenBy(w => FindIndex(repos, w.Repository))
                .ThenBy(w => w.Repository, comparer: StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(w => (int)w.Priority)
                .ThenBy(w => w.FirstSeen),
        ];

        DateTimeOffset asOf = combined.Count > 0 ? combined.Max(w => w.LastUpdated) : now;
        long lagSeconds = Math.Max(0, (long)(now - asOf).TotalSeconds);

        return new PrioritiesResponse(Priorities: ordered, AsOf: asOf, LagSeconds: lagSeconds);
    }

    private static async Task<List<WorkItem>> BuildIssuesAsync(
        DispatcherDbContext context,
        IReadOnlyList<string> owners,
        IReadOnlyList<string> repos,
        int maxIssues,
        CancellationToken cancellationToken
    )
    {
        List<IssueEntity> issueEntities = await context
            .Issues.Where(e =>
                e.Status != ClosedStatus
                && !e.IsOnHold
                && !context.Repos.Any(r => r.Repository == e.Repository && !r.IsActive)
                && !context.PullRequests.Any(pr => pr.Repository == e.Repository && pr.Status != ClosedStatus)
            )
            .ToListAsync(cancellationToken);

        IEnumerable<WorkItem> topIssuePerRepo = issueEntities
            .GroupBy(e => e.Repository, comparer: StringComparer.Ordinal)
            .Select(g => MapIssue(g.OrderByDescending(e => (int)e.Priority).ThenBy(e => e.FirstSeen).First()));

        return ApplyMaxIssuesCap(
            topIssuePerRepo
                .OrderBy(w => FindIndex(owners, GetOwner(w.Repository)))
                .ThenBy(w => GetOwner(w.Repository), comparer: StringComparer.OrdinalIgnoreCase)
                .ThenBy(w => FindIndex(repos, w.Repository))
                .ThenBy(w => w.Repository, comparer: StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(w => (int)w.Priority)
                .ThenBy(w => w.FirstSeen),
            maxIssues
        );
    }

    private static List<WorkItem> ApplyMaxIssuesCap(IOrderedEnumerable<WorkItem> ordered, int maxIssues)
    {
        return maxIssues > 0 ? [.. ordered.Take(maxIssues)] : [.. ordered];
    }

    private static List<WorkItem> Deduplicate(IReadOnlyList<WorkItem> items)
    {
        // Defensive: a PR and an Issue can share the same numeric Id in the same repo.
        // DistinctBy ensures the merged list never surfaces the same (Repository, Id) pair twice.
        return [.. items.DistinctBy(static w => (w.Repository, w.Id))];
    }

    private static WorkItem MapPullRequest(
        PullRequestEntity entity,
        in DateTimeOffset now,
        in TimeSpan stuckDependabotTimeout
    )
    {
        WorkPriority priority = IsStuckDependabotPullRequest(
            entity: entity,
            now: now,
            stuckDependabotTimeout: stuckDependabotTimeout
        )
            ? WorkPriority.Security
            : entity.Priority;

        return new WorkItem(
            Repository: entity.Repository,
            Id: entity.Id,
            ItemType: PullRequestType,
            Priority: priority,
            FirstSeen: entity.FirstSeen,
            LastUpdated: entity.LastUpdated,
            Status: entity.Status,
            WhenClosed: entity.WhenClosed,
            IsOnHold: entity.IsOnHold,
            LinkedPrNumbers: [],
            CommentCount: entity.CommentCount,
            ReviewDecision: MapReviewDecision(entity.ReviewDecision, isPullRequest: true),
            FailedCheckCount: entity.FailedCheckCount,
            FailedCheckNames: SplitNames(entity.FailedCheckNames),
            FailedCheckSha: entity.FailedCheckSha,
            Author: entity.Author
        );
    }

    private static bool IsStuckDependabotPullRequest(
        PullRequestEntity entity,
        in DateTimeOffset now,
        in TimeSpan stuckDependabotTimeout
    )
    {
        if (stuckDependabotTimeout <= TimeSpan.Zero)
        {
            return false;
        }

        if (!string.Equals(a: entity.Author, b: DependabotLogin, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return now - entity.FirstSeen >= stuckDependabotTimeout;
    }

    private static WorkItem MapIssue(IssueEntity e)
    {
        return new WorkItem(
            Repository: e.Repository,
            Id: e.Id,
            ItemType: IssueType,
            Priority: e.Priority,
            FirstSeen: e.FirstSeen,
            LastUpdated: e.LastUpdated,
            Status: e.Status,
            WhenClosed: e.WhenClosed,
            IsOnHold: e.IsOnHold,
            LinkedPrNumbers: e.LinkedPrNumber.HasValue ? [e.LinkedPrNumber.Value] : [],
            CommentCount: 0,
            ReviewDecision: ReviewDecisionState.NotApplicable,
            FailedCheckCount: 0,
            FailedCheckNames: [],
            FailedCheckSha: null,
            Author: null
        );
    }

    private static ReviewDecisionState MapReviewDecision(string? reviewDecision, bool isPullRequest)
    {
        if (!isPullRequest)
        {
            return ReviewDecisionState.NotApplicable;
        }

        if (string.IsNullOrEmpty(reviewDecision))
        {
            return ReviewDecisionState.NotReviewed;
        }

        if (string.Equals(a: reviewDecision, b: "Approved", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return ReviewDecisionState.Approved;
        }

        if (string.Equals(a: reviewDecision, b: "ChangesRequested", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return ReviewDecisionState.ChangesRequested;
        }

        return ReviewDecisionState.NotReviewed;
    }

    private static ImmutableArray<string> SplitNames(string? names)
    {
        if (string.IsNullOrEmpty(names))
        {
            return [];
        }

        return [.. names.Split(',', StringSplitOptions.RemoveEmptyEntries)];
    }

    private static bool IsPullRequest(WorkItem w)
    {
        return string.Equals(a: w.ItemType, b: PullRequestType, comparisonType: StringComparison.Ordinal);
    }

    private static int FindIndex(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(a: list[i], b: value, comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static string GetOwner(string repository)
    {
        int slash = repository.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);

        return slash >= 0 ? repository[..slash] : repository;
    }
}
