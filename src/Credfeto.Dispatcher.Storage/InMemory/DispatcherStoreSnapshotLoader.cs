using Credfeto.Dispatcher.Storage.InMemory.LoggingExtensions;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.Storage.InMemory;

public sealed class DispatcherStoreSnapshotLoader : IDispatcherStoreSnapshotLoader
{
    private readonly InMemoryDispatcherStore _store;
    private readonly DispatcherStoreSnapshotStore _snapshotStore;
    private readonly ILogger<DispatcherStoreSnapshotLoader> _logger;

    public DispatcherStoreSnapshotLoader(
        InMemoryDispatcherStore store,
        DispatcherStoreSnapshotStore snapshotStore,
        ILogger<DispatcherStoreSnapshotLoader> logger
    )
    {
        this._store = store;
        this._snapshotStore = snapshotStore;
        this._logger = logger;
    }

    public void LoadSnapshot()
    {
        if (!this._snapshotStore.TryLoad(out DispatcherStoreSnapshotData? data))
        {
            this._logger.SnapshotNotLoaded();

            return;
        }

        this._store.ImportSnapshot(data);
        this._logger.SnapshotLoaded(pullRequestCount: data.PullRequests.Length, issueCount: data.Issues.Length);
    }
}
