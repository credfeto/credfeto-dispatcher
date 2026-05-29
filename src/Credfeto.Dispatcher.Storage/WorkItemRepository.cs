using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Database;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage;

public sealed class WorkItemRepository : IWorkItemRepository
{
    private const string PULL_REQUEST_TYPE = "PullRequest";
    private const string ISSUE_TYPE = "Issue";
    private const string DEPENDABOT_LOGIN = "dependabot[bot]";

    private readonly IDatabase _database;
    private readonly TimeProvider _timeProvider;

    public WorkItemRepository(IDatabase database, TimeProvider timeProvider)
    {
        this._database = database;
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
        IReadOnlyList<PullRequestRow> prRows = await this._database.ExecuteAsync(
            action: DispatcherDatabase.PullRequests_GetActiveAsync,
            cancellationToken: cancellationToken
        );

        IReadOnlyList<IssueRow> issueRows = await this._database.ExecuteAsync(
            action: DispatcherDatabase.Issues_GetActiveAsync,
            cancellationToken: cancellationToken
        );

        DateTimeOffset now = this._timeProvider.GetUtcNow();

        List<WorkItem> pullRequests =
        [
            .. prRows.Select(row => MapPullRequest(row: row, now: now, stuckDependabotTimeout: stuckDependabotTimeout)),
        ];

        List<WorkItem> issues = BuildIssues(issueRows: issueRows, owners: owners, repos: repos, maxIssues: maxIssues);

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

    private static List<WorkItem> BuildIssues(
        IReadOnlyList<IssueRow> issueRows,
        IReadOnlyList<string> owners,
        IReadOnlyList<string> repos,
        int maxIssues
    )
    {
        IEnumerable<WorkItem> topIssuePerRepo = issueRows
            .GroupBy(e => e.Repository, comparer: StringComparer.Ordinal)
            .Select(g => MapIssue(g.OrderByDescending(e => e.Priority).ThenBy(e => e.FirstSeen).First()));

        IEnumerable<WorkItem> ordered = topIssuePerRepo
            .OrderBy(w => FindIndex(owners, GetOwner(w.Repository)))
            .ThenBy(w => GetOwner(w.Repository), comparer: StringComparer.OrdinalIgnoreCase)
            .ThenBy(w => FindIndex(repos, w.Repository))
            .ThenBy(w => w.Repository, comparer: StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(w => (int)w.Priority)
            .ThenBy(w => w.FirstSeen);

        return ApplyMaxIssuesCap(ordered, maxIssues);
    }

    private static List<WorkItem> ApplyMaxIssuesCap(IEnumerable<WorkItem> ordered, int maxIssues)
    {
        if (maxIssues <= 0)
        {
            return [.. ordered];
        }

        List<WorkItem> all = [.. ordered];
        List<WorkItem> highPriority = [.. all.Where(static w => w.Priority >= WorkPriority.URGENT)];
        List<WorkItem> regular = [.. all.Where(static w => w.Priority < WorkPriority.URGENT).Take(maxIssues)];

        return [.. highPriority, .. regular];
    }

    private static List<WorkItem> Deduplicate(IReadOnlyList<WorkItem> items)
    {
        return [.. items.DistinctBy(static w => (w.Repository, w.Id))];
    }

    private static WorkItem MapPullRequest(
        PullRequestRow row,
        in DateTimeOffset now,
        in TimeSpan stuckDependabotTimeout
    )
    {
        WorkPriority priority = (WorkPriority)row.Priority;

        if (IsStuckDependabotPullRequest(row: row, now: now, stuckDependabotTimeout: stuckDependabotTimeout))
        {
            priority = WorkPriority.SECURITY;
        }

        return new WorkItem(
            Repository: row.Repository,
            Id: row.Id,
            ItemType: PULL_REQUEST_TYPE,
            Priority: priority,
            FirstSeen: row.FirstSeen,
            LastUpdated: row.LastUpdated,
            Status: row.Status,
            WhenClosed: row.WhenClosed,
            IsOnHold: row.IsOnHold,
            LinkedPrNumbers: [],
            CommentCount: row.CommentCount,
            ReviewDecision: MapReviewDecision(row.ReviewDecision, isPullRequest: true),
            FailedCheckCount: row.FailedCheckCount,
            FailedCheckNames: SplitNames(row.FailedCheckNames),
            FailedCheckSha: row.FailedCheckSha,
            Author: row.Author
        );
    }

    private static bool IsStuckDependabotPullRequest(
        PullRequestRow row,
        in DateTimeOffset now,
        in TimeSpan stuckDependabotTimeout
    )
    {
        if (stuckDependabotTimeout <= TimeSpan.Zero)
        {
            return false;
        }

        if (!string.Equals(a: row.Author, b: DEPENDABOT_LOGIN, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return now - row.FirstSeen >= stuckDependabotTimeout;
    }

    private static WorkItem MapIssue(IssueRow row)
    {
        return new WorkItem(
            Repository: row.Repository,
            Id: row.Id,
            ItemType: ISSUE_TYPE,
            Priority: (WorkPriority)row.Priority,
            FirstSeen: row.FirstSeen,
            LastUpdated: row.LastUpdated,
            Status: row.Status,
            WhenClosed: row.WhenClosed,
            IsOnHold: row.IsOnHold,
            LinkedPrNumbers: row.LinkedPrNumber.HasValue ? [row.LinkedPrNumber.Value] : [],
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

    private static bool IsPullRequest(WorkItem w)
    {
        return string.Equals(a: w.ItemType, b: PULL_REQUEST_TYPE, comparisonType: StringComparison.Ordinal);
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

    public async ValueTask RemoveItemsForRepositoriesAsync(
        IReadOnlyList<string> repositories,
        CancellationToken cancellationToken
    )
    {
        if (repositories.Count == 0)
        {
            return;
        }

        string reposCsv = string.Join(separator: ',', repositories);

        await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.PullRequests_RemoveForRepositoriesAsync(
                    connection: c,
                    repositories: reposCsv,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );

        await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.Issues_RemoveForRepositoriesAsync(
                    connection: c,
                    repositories: reposCsv,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }

    public async ValueTask CloseStaleItemsForRepoAsync(
        string repository,
        IReadOnlyList<int> activePullRequestNumbers,
        IReadOnlyList<int> activeIssueNumbers,
        CancellationToken cancellationToken
    )
    {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        string? activePrIds =
            activePullRequestNumbers.Count > 0 ? string.Join(separator: ',', activePullRequestNumbers) : null;
        string? activeIssueIds = activeIssueNumbers.Count > 0 ? string.Join(separator: ',', activeIssueNumbers) : null;

        await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.PullRequests_CloseStaleAsync(
                    connection: c,
                    repository: repository,
                    activePrIds: activePrIds,
                    now: now,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );

        await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.Issues_CloseStaleAsync(
                    connection: c,
                    repository: repository,
                    activeIssueIds: activeIssueIds,
                    now: now,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }

    private static string GetOwner(string repository)
    {
        int slash = repository.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);

        return slash >= 0 ? repository[..slash] : repository;
    }
}
