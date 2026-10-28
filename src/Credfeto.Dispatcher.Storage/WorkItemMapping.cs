using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage;

internal static class WorkItemMapping
{
    private const string PULL_REQUEST_TYPE = "PullRequest";
    private const string ISSUE_TYPE = "Issue";

    public static DbStateResponse BuildDatabaseState(
        IReadOnlyList<PullRequestStateRow> prRows,
        IReadOnlyList<IssueStateRow> issueRows,
        in DateTimeOffset now
    )
    {
        IReadOnlyList<PullRequestState> pullRequests = [.. prRows.Select(MapPullRequestState)];
        IReadOnlyList<IssueState> issues = [.. issueRows.Select(MapIssueState)];

        DateTimeOffset asOf = pullRequests
            .Select(static p => p.LastUpdated)
            .Concat(issues.Select(static i => i.LastUpdated))
            .DefaultIfEmpty(now)
            .Max();

        return new DbStateResponse(PullRequests: pullRequests, Issues: issues, AsOf: asOf);
    }

    private static PullRequestState MapPullRequestState(PullRequestStateRow row)
    {
        (string owner, string repo) = SplitRepository(row.Repository);

        return new PullRequestState(
            Owner: owner,
            Repo: repo,
            Repository: row.Repository,
            Number: row.Id,
            Status: row.Status,
            Priority: (WorkPriority)row.Priority,
            IsOnHold: row.IsOnHold,
            FirstSeen: row.FirstSeen,
            LastUpdated: row.LastUpdated,
            WhenClosed: row.WhenClosed,
            Assignees: SplitNames(row.Assignees),
            CommentCount: row.CommentCount,
            ReviewDecision: MapReviewDecision(row.ReviewDecision, isPullRequest: true),
            FailedCheckCount: row.FailedCheckCount,
            FailedCheckNames: ParseFailedCheckNames(row.FailedCheckNames),
            FailedCheckSha: row.FailedCheckSha,
            Author: row.Author,
            IsAdopted: row.IsAdopted
        );
    }

    private static IssueState MapIssueState(IssueStateRow row)
    {
        (string owner, string repo) = SplitRepository(row.Repository);

        return new IssueState(
            Owner: owner,
            Repo: repo,
            Repository: row.Repository,
            Number: row.Id,
            Status: row.Status,
            Priority: (WorkPriority)row.Priority,
            IsOnHold: row.IsOnHold,
            FirstSeen: row.FirstSeen,
            LastUpdated: row.LastUpdated,
            WhenClosed: row.WhenClosed,
            Assignees: SplitNames(row.Assignees),
            LinkedPrNumber: row.LinkedPrNumber
        );
    }

    internal static (string Owner, string Repo) SplitRepository(string repository)
    {
        int slash = repository.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);

        return slash >= 0 ? (repository[..slash], repository[(slash + 1)..]) : (repository, string.Empty);
    }

    public static PrioritiesResponse BuildPrioritiesResponse(IReadOnlyList<WorkItem> ordered, in DateTimeOffset now)
    {
        DateTimeOffset asOf = ordered.Count > 0 ? ordered.Max(static w => w.LastUpdated) : now;
        long lagSeconds = Math.Max(0, (long)(now - asOf).TotalSeconds);

        return new PrioritiesResponse(Priorities: ordered, AsOf: asOf, LagSeconds: lagSeconds);
    }

    public static bool IsPullRequest(string itemType)
    {
        return string.Equals(a: itemType, b: PULL_REQUEST_TYPE, comparisonType: StringComparison.Ordinal);
    }

    public static WorkItem MapWorkItem(WorkItemRow row)
    {
        if (IsPullRequest(row.ItemType))
        {
            return BuildPullRequest(
                repository: row.Repository,
                id: row.Id,
                status: row.Status,
                firstSeen: row.FirstSeen,
                lastUpdated: row.LastUpdated,
                whenClosed: row.WhenClosed,
                priority: row.Priority,
                isOnHold: row.IsOnHold,
                commentCount: row.CommentCount,
                reviewDecision: row.ReviewDecision,
                failedCheckCount: row.FailedCheckCount,
                failedCheckNames: row.FailedCheckNames,
                failedCheckSha: row.FailedCheckSha,
                author: row.Author
            );
        }

        return BuildIssue(
            repository: row.Repository,
            id: row.Id,
            status: row.Status,
            firstSeen: row.FirstSeen,
            lastUpdated: row.LastUpdated,
            whenClosed: row.WhenClosed,
            priority: row.Priority,
            isOnHold: row.IsOnHold,
            linkedPrNumber: row.LinkedPrNumber
        );
    }

    public static WorkItem MapPullRequest(PullRequestRow row)
    {
        return BuildPullRequest(
            repository: row.Repository,
            id: row.Id,
            status: row.Status,
            firstSeen: row.FirstSeen,
            lastUpdated: row.LastUpdated,
            whenClosed: row.WhenClosed,
            priority: row.Priority,
            isOnHold: row.IsOnHold,
            commentCount: row.CommentCount,
            reviewDecision: row.ReviewDecision,
            failedCheckCount: row.FailedCheckCount,
            failedCheckNames: row.FailedCheckNames,
            failedCheckSha: row.FailedCheckSha,
            author: row.Author
        );
    }

    public static WorkItem MapIssue(IssueRow row)
    {
        return BuildIssue(
            repository: row.Repository,
            id: row.Id,
            status: row.Status,
            firstSeen: row.FirstSeen,
            lastUpdated: row.LastUpdated,
            whenClosed: row.WhenClosed,
            priority: row.Priority,
            isOnHold: row.IsOnHold,
            linkedPrNumber: row.LinkedPrNumber
        );
    }

    private static WorkItem BuildPullRequest(
        string repository,
        int id,
        string status,
        in DateTimeOffset firstSeen,
        in DateTimeOffset lastUpdated,
        DateTimeOffset? whenClosed,
        int priority,
        bool isOnHold,
        int commentCount,
        string? reviewDecision,
        int failedCheckCount,
        string? failedCheckNames,
        string? failedCheckSha,
        string? author
    )
    {
        return new WorkItem(
            Repository: repository,
            Id: id,
            ItemType: PULL_REQUEST_TYPE,
            Priority: (WorkPriority)priority,
            FirstSeen: firstSeen,
            LastUpdated: lastUpdated,
            Status: status,
            WhenClosed: whenClosed,
            IsOnHold: isOnHold,
            LinkedPrNumbers: [],
            CommentCount: commentCount,
            ReviewDecision: MapReviewDecision(reviewDecision, isPullRequest: true),
            FailedCheckCount: failedCheckCount,
            FailedCheckNames: ParseFailedCheckNames(failedCheckNames),
            FailedCheckSha: failedCheckSha,
            Author: author
        );
    }

    private static WorkItem BuildIssue(
        string repository,
        int id,
        string status,
        in DateTimeOffset firstSeen,
        in DateTimeOffset lastUpdated,
        DateTimeOffset? whenClosed,
        int priority,
        bool isOnHold,
        int? linkedPrNumber
    )
    {
        return new WorkItem(
            Repository: repository,
            Id: id,
            ItemType: ISSUE_TYPE,
            Priority: (WorkPriority)priority,
            FirstSeen: firstSeen,
            LastUpdated: lastUpdated,
            Status: status,
            WhenClosed: whenClosed,
            IsOnHold: isOnHold,
            LinkedPrNumbers: linkedPrNumber.HasValue ? [linkedPrNumber.Value] : [],
            CommentCount: 0,
            ReviewDecision: ReviewDecisionState.NOT_APPLICABLE,
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
            return ReviewDecisionState.NOT_APPLICABLE;
        }

        if (string.IsNullOrEmpty(reviewDecision))
        {
            return ReviewDecisionState.NOT_REVIEWED;
        }

        if (string.Equals(a: reviewDecision, b: "Approved", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return ReviewDecisionState.APPROVED;
        }

        if (string.Equals(a: reviewDecision, b: "ChangesRequested", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return ReviewDecisionState.CHANGES_REQUESTED;
        }

        return ReviewDecisionState.NOT_REVIEWED;
    }

    private static ImmutableArray<string> SplitNames(string? names)
    {
        if (string.IsNullOrEmpty(names))
        {
            return [];
        }

        return [.. names.Split(',', StringSplitOptions.RemoveEmptyEntries)];
    }

    private static ImmutableArray<string> ParseFailedCheckNames(string? names)
    {
        if (string.IsNullOrEmpty(names))
        {
            return [];
        }

        if (!names.StartsWith('['))
        {
            return SplitNames(names);
        }

        try
        {
            return JsonSerializer.Deserialize(
                json: names,
                jsonTypeInfo: CheckNamesSerializerContext.Default.ImmutableArrayString
            );
        }
        catch (JsonException)
        {
            // Rows written before the JSON format may hold a comma list whose first name starts with '['.
            return SplitNames(names);
        }
    }
}
