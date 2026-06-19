using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Database;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage;

public sealed class ActiveRepoTracker : IActiveRepoTracker
{
    private readonly IDatabase _database;

    public ActiveRepoTracker(IDatabase database)
    {
        this._database = database;
    }

    public async ValueTask<IReadOnlyList<string>> GetActiveReposAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<RepoRow> rows = await this._database.ExecuteAsync(
            action: DispatcherDatabase.Repos_GetActiveAsync,
            cancellationToken: cancellationToken
        );

        return [.. rows.Select(static r => r.Repository)];
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
