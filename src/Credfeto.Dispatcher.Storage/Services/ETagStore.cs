using System.Threading;
using System.Threading.Tasks;
using Credfeto.Database;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage.Services;

public sealed class ETagStore : IETagStore
{
    private readonly IDatabase _database;

    public ETagStore(IDatabase database)
    {
        this._database = database;
    }

    public async ValueTask<string?> GetETagAsync(string key, CancellationToken cancellationToken)
    {
        PollingStateRow? row = await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.PollingStates_GetByKeyAsync(connection: c, key: key, cancellationToken: ct),
            cancellationToken: cancellationToken
        );

        return row?.ETag;
    }

    public ValueTask SaveETagAsync(string key, string eTag, CancellationToken cancellationToken)
    {
        return this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.PollingStates_UpsertAsync(
                    connection: c,
                    key: key,
                    eTag: eTag,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }
}
