using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Database;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database;

namespace Credfeto.Dispatcher.Storage;

public sealed class ActiveRepoTracker : IActiveRepoTracker
{
    private readonly IDatabase _database;
    private readonly TimeProvider _timeProvider;

    public ActiveRepoTracker(IDatabase database, TimeProvider timeProvider)
    {
        this._database = database;
        this._timeProvider = timeProvider;
    }

    public ValueTask UpdateActiveReposAsync(IReadOnlyList<string> activeRepos, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        string repositories = string.Join(separator: ',', activeRepos);

        return this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.Repos_SetActiveAsync(
                    connection: c,
                    repositories: repositories,
                    lastUpdated: now,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }
}
