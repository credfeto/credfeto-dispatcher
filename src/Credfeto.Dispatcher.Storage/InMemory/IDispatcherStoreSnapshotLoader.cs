namespace Credfeto.Dispatcher.Storage.InMemory;

// Public (unlike the rest of this feature's types) because ServerStartup, in a different
// project, resolves this directly to run the synchronous startup load - see the ordering note
// in ServerStartup.CreateApp. Every provider registers an implementation - DispatcherStoreSnapshotLoader
// under DatabaseProvider.InMemory, NullDispatcherStoreSnapshotLoader under DatabaseProvider.SqlServer -
// so ServerStartup can resolve it via `app.Services.GetRequiredService<IDispatcherStoreSnapshotLoader>()`
// without needing to know which provider is configured.
public interface IDispatcherStoreSnapshotLoader
{
    void LoadSnapshot();
}
