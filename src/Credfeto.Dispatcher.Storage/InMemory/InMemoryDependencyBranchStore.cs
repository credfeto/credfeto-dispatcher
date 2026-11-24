using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;

namespace Credfeto.Dispatcher.Storage.InMemory;

public sealed class InMemoryDependencyBranchStore : IDependencyBranchStore
{
    private readonly InMemoryDispatcherStore _store;

    public InMemoryDependencyBranchStore(InMemoryDispatcherStore store)
    {
        this._store = store;
    }

    public ValueTask RecordCreatedAsync(
        string repository,
        string branch,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken
    )
    {
        this._store.RecordDependencyBranchCreated(repository: repository, branch: branch, createdAt: createdAt);

        return ValueTask.CompletedTask;
    }

    public ValueTask RecordDeletedAsync(
        string repository,
        string branch,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken
    )
    {
        this._store.RecordDependencyBranchDeleted(repository: repository, branch: branch, deletedAt: deletedAt);

        return ValueTask.CompletedTask;
    }

    public ValueTask RecordPullRequestAsync(
        string repository,
        string branch,
        int pullRequestNumber,
        CancellationToken cancellationToken
    )
    {
        this._store.RecordDependencyBranchPullRequest(
            repository: repository,
            branch: branch,
            pullRequestNumber: pullRequestNumber
        );

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<DependencyBranchRecheck>> GetDueForRecheckAsync(
        int maxCount,
        DateTimeOffset checkedBefore,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<DependencyBranchRecheck> due =
        [
            .. this
                ._store.GetDependencyBranchesDueForRecheck(maxCount: maxCount, checkedBefore: checkedBefore)
                .Select(WorkItemMapping.MapDependencyBranchRecheck),
        ];

        return ValueTask.FromResult(due);
    }

    public ValueTask RecordCheckedAsync(
        string repository,
        string branch,
        string? eTag,
        CancellationToken cancellationToken
    )
    {
        this._store.RecordDependencyBranchChecked(repository: repository, branch: branch, eTag: eTag);

        return ValueTask.CompletedTask;
    }

    public ValueTask PruneAsync(
        DateTimeOffset settledBefore,
        DateTimeOffset firstSeenBefore,
        CancellationToken cancellationToken
    )
    {
        this._store.PruneDependencyBranches(settledBefore: settledBefore, firstSeenBefore: firstSeenBefore);

        return ValueTask.CompletedTask;
    }
}
