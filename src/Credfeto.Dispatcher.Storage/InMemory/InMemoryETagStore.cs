using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;

namespace Credfeto.Dispatcher.Storage.InMemory;

public sealed class InMemoryETagStore : IETagStore
{
    private readonly InMemoryDispatcherStore _store;

    public InMemoryETagStore(InMemoryDispatcherStore store)
    {
        this._store = store;
    }

    public ValueTask<string?> GetETagAsync(string key, CancellationToken cancellationToken)
    {
        return new ValueTask<string?>(this._store.GetETag(key));
    }

    public ValueTask SaveETagAsync(string key, string eTag, CancellationToken cancellationToken)
    {
        this._store.SaveETag(key: key, eTag: eTag);

        return ValueTask.CompletedTask;
    }
}
