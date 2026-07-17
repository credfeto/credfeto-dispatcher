using System;
using System.Collections.Generic;
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
        CancellationToken cancellationToken
    )
    {
        (IReadOnlyList<PullRequestRow> prRows, IReadOnlyList<IssueRow> issueRows) = this._store.GetActiveWorkItems();

        PrioritiesResponse response = WorkItemMapping.BuildResponse(
            prRows: prRows,
            issueRows: issueRows,
            owners: owners,
            maxIssues: maxIssues,
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
