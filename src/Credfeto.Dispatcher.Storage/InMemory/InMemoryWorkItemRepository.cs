using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage.InMemory;

public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private readonly InMemoryDispatcherStore _store;
    private readonly TimeProvider _timeProvider;

    public InMemoryWorkItemRepository(InMemoryDispatcherStore store, TimeProvider timeProvider)
    {
        this._store = store;
        this._timeProvider = timeProvider;
    }

    public Task<PrioritiesResponse> GetPrioritisedWorkItemsAsync(
        IReadOnlyList<string> owners,
        int maxIssues,
        IReadOnlyList<string> boostedRepos,
        string? user,
        CancellationToken cancellationToken
    )
    {
        (IReadOnlyList<PullRequestRow> prRows, IReadOnlyList<IssueRow> issueRows) = this._store.GetActiveWorkItems(
            user
        );

        Dictionary<string, int> ownersIndex = BuildIndex(owners);
        Dictionary<string, int> boostedIndex = BuildIndex(boostedRepos);

        List<WorkItem> pullRequests = [.. prRows.Select(WorkItemMapping.MapPullRequest)];
        IReadOnlyList<WorkItem> issues = BuildIssues(
            issueRows: issueRows,
            ownersIndex: ownersIndex,
            maxIssues: maxIssues
        );

        IReadOnlyList<WorkItem> ordered =
        [
            .. pullRequests
                .Concat(issues)
                .DistinctBy(static w => (w.Repository, w.Id))
                .OrderBy(static w => !IsPullRequest(w))
                .ThenBy(static w => !IsHighPriority(w))
                .ThenBy(w => boostedIndex.GetValueOrDefault(w.Repository, int.MaxValue))
                .ThenBy(w => ownersIndex.GetValueOrDefault(GetOwner(w.Repository), int.MaxValue))
                .ThenBy(static w => GetOwner(w.Repository), comparer: StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(static w => (int)w.Priority)
                .ThenBy(static w => w.FirstSeen),
        ];

        PrioritiesResponse response = WorkItemMapping.BuildPrioritiesResponse(
            ordered: ordered,
            now: this._timeProvider.GetUtcNow()
        );

        return Task.FromResult(response);
    }

    private static Dictionary<string, int> BuildIndex(IReadOnlyList<string> values)
    {
        Dictionary<string, int> index = new(values.Count, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < values.Count; ++i)
        {
            index.TryAdd(values[i], i);
        }

        return index;
    }

    private static IReadOnlyList<WorkItem> BuildIssues(
        IReadOnlyList<IssueRow> issueRows,
        Dictionary<string, int> ownersIndex,
        int maxIssues
    )
    {
        IReadOnlyList<WorkItem> ordered =
        [
            .. issueRows
                .GroupBy(static e => e.Repository, comparer: StringComparer.Ordinal)
                .Select(static g =>
                    WorkItemMapping.MapIssue(
                        g.OrderByDescending(static e => e.Priority).ThenBy(static e => e.FirstSeen).First()
                    )
                )
                .OrderBy(w => ownersIndex.GetValueOrDefault(GetOwner(w.Repository), int.MaxValue))
                .ThenBy(static w => GetOwner(w.Repository), comparer: StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(static w => (int)w.Priority)
                .ThenBy(static w => w.FirstSeen),
        ];

        // maxIssues <= 0 means no cap. The cap applies per owner; URGENT and above always pass.
        if (maxIssues <= 0)
        {
            return ordered;
        }

        IEnumerable<WorkItem> highPriority = ordered.Where(IsHighPriority);
        IEnumerable<WorkItem> regular = ordered
            .Where(static w => !IsHighPriority(w))
            .GroupBy(static w => GetOwner(w.Repository), comparer: StringComparer.OrdinalIgnoreCase)
            .SelectMany(g => g.Take(maxIssues));

        return [.. highPriority, .. regular];
    }

    private static bool IsHighPriority(WorkItem w)
    {
        return w.Priority >= WorkPriority.URGENT;
    }

    private static bool IsPullRequest(WorkItem w)
    {
        return string.Equals(a: w.ItemType, b: "PullRequest", comparisonType: StringComparison.Ordinal);
    }

    private static string GetOwner(string repository)
    {
        return WorkItemMapping.SplitRepository(repository).Owner;
    }

    public Task<DbStateResponse> GetDatabaseStateAsync(
        string? owner,
        string? repo,
        string? user,
        CancellationToken cancellationToken
    )
    {
        (IReadOnlyList<PullRequestStateRow> prRows, IReadOnlyList<IssueStateRow> issueRows) =
            this._store.GetAllWorkItems(owner: owner, repo: repo, user: user);

        DbStateResponse response = WorkItemMapping.BuildDatabaseState(
            prRows: prRows,
            issueRows: issueRows,
            now: this._timeProvider.GetUtcNow()
        );

        return Task.FromResult(response);
    }

    public ValueTask RemoveItemsForRepositoriesAsync(
        IReadOnlyList<string> repositories,
        CancellationToken cancellationToken
    )
    {
        this._store.RemoveForRepositories(repositories);

        return ValueTask.CompletedTask;
    }

    public ValueTask CloseStaleItemsForRepoAsync(
        string repository,
        IReadOnlyList<int> activePullRequestNumbers,
        IReadOnlyList<int> activeIssueNumbers,
        CancellationToken cancellationToken
    )
    {
        this._store.CloseStalePullRequests(
            repository: repository,
            activeIds: activePullRequestNumbers.Count > 0 ? activePullRequestNumbers : null
        );

        this._store.CloseStaleIssues(
            repository: repository,
            activeIds: activeIssueNumbers.Count > 0 ? activeIssueNumbers : null
        );

        return ValueTask.CompletedTask;
    }
}
