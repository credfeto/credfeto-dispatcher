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

    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;

    public WorkItemRepository(IDbContextFactory<DispatcherDbContext> dbContextFactory)
    {
        this._dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<WorkItem>> GetPrioritisedWorkItemsAsync(
        IReadOnlyList<string> owners,
        IReadOnlyList<string> repos,
        CancellationToken cancellationToken
    )
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(
            cancellationToken
        );

        List<PullRequestEntity> prEntities = await context
            .PullRequests.Where(e => e.Status != ClosedStatus && !e.IsOnHold)
            .ToListAsync(cancellationToken);

        List<IssueEntity> issueEntities = await context
            .Issues.Where(e => e.Status != ClosedStatus && !e.IsOnHold && e.LinkedPrNumber == null)
            .ToListAsync(cancellationToken);

        List<WorkItem> pullRequests = [.. prEntities.Select(MapPullRequest)];
        List<WorkItem> issues = [.. issueEntities.Select(MapIssue)];

        List<WorkItem> combined = [.. pullRequests, .. issues];

        return
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
    }

    private static WorkItem MapPullRequest(PullRequestEntity e)
    {
        return new WorkItem(
            Repository: e.Repository,
            Id: e.Id,
            ItemType: PullRequestType,
            Priority: e.Priority,
            FirstSeen: e.FirstSeen,
            LastUpdated: e.LastUpdated,
            Status: e.Status,
            WhenClosed: e.WhenClosed,
            IsOnHold: e.IsOnHold,
            LinkedPrNumbers: [],
            CommentCount: e.CommentCount,
            ReviewDecision: MapReviewDecision(e.ReviewDecision, isPullRequest: true),
            FailedCheckCount: e.FailedCheckCount,
            FailedCheckNames: SplitNames(e.FailedCheckNames),
            FailedCheckSha: e.FailedCheckSha
        );
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
            FailedCheckSha: null
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

        if (
            string.Equals(
                a: reviewDecision,
                b: "Approved",
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return ReviewDecisionState.Approved;
        }

        if (
            string.Equals(
                a: reviewDecision,
                b: "ChangesRequested",
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
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
        return string.Equals(
            a: w.ItemType,
            b: PullRequestType,
            comparisonType: StringComparison.Ordinal
        );
    }

    private static int FindIndex(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (
                string.Equals(
                    a: list[i],
                    b: value,
                    comparisonType: StringComparison.OrdinalIgnoreCase
                )
            )
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
