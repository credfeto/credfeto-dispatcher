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

    public ActiveRepoTracker(IDatabase database)
    {
        this._database = database;
    }

    public ValueTask UpdateActiveReposAsync(IReadOnlyList<string> activeRepos, CancellationToken cancellationToken)
    {
        string repositories = string.Join(separator: ',', activeRepos);

        return this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.Repos_SetActiveAsync(
                    connection: c,
                    repositories: repositories,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }
}
