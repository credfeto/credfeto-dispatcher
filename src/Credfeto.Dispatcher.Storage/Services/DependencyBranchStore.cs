using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Database;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage.Services;

public sealed class DependencyBranchStore : IDependencyBranchStore
{
    private readonly IDatabase _database;

    public DependencyBranchStore(IDatabase database)
    {
        this._database = database;
    }

    public ValueTask RecordCreatedAsync(
        string repository,
        string branch,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken
    )
    {
        return this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.DependencyBranches_RecordCreatedAsync(
                    connection: c,
                    repository: repository,
                    branch: branch,
                    createdAt: createdAt,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }

    public ValueTask RecordDeletedAsync(
        string repository,
        string branch,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken
    )
    {
        return this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.DependencyBranches_RecordDeletedAsync(
                    connection: c,
                    repository: repository,
                    branch: branch,
                    deletedAt: deletedAt,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }

    public ValueTask RecordPullRequestAsync(
        string repository,
        string branch,
        int pullRequestNumber,
        CancellationToken cancellationToken
    )
    {
        return this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.DependencyBranches_RecordPullRequestAsync(
                    connection: c,
                    repository: repository,
                    branch: branch,
                    pullRequestNumber: pullRequestNumber,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }

    public async ValueTask<IReadOnlyList<DependencyBranchRecheck>> GetDueForRecheckAsync(
        int maxCount,
        DateTimeOffset checkedBefore,
        CancellationToken cancellationToken
    )
    {
        if (maxCount <= 0)
        {
            return [];
        }

        IReadOnlyList<DependencyBranchRow> rows = await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.DependencyBranches_GetDueForRecheckAsync(
                    connection: c,
                    maxCount: maxCount,
                    checkedBefore: checkedBefore,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );

        return [.. rows.Select(WorkItemMapping.MapDependencyBranchRecheck)];
    }

    public ValueTask RecordCheckedAsync(
        string repository,
        string branch,
        string? eTag,
        CancellationToken cancellationToken
    )
    {
        return this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.DependencyBranches_RecordCheckedAsync(
                    connection: c,
                    repository: repository,
                    branch: branch,
                    eTag: eTag,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }

    public ValueTask PruneAsync(
        DateTimeOffset settledBefore,
        DateTimeOffset firstSeenBefore,
        CancellationToken cancellationToken
    )
    {
        return this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.DependencyBranches_PruneAsync(
                    connection: c,
                    settledBefore: settledBefore,
                    firstSeenBefore: firstSeenBefore,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }
}
