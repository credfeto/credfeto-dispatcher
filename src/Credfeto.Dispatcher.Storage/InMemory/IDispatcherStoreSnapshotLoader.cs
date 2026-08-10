namespace Credfeto.Dispatcher.Storage.InMemory;

// Public (unlike the rest of this feature's types) because ServerStartup, in a different
// project, resolves this directly to run the synchronous startup load - see the ordering note
// in ServerStartup.CreateApp. Only ever registered under DatabaseProvider.InMemory, so
// ServerStartup can call this via `app.Services.GetService<IDispatcherStoreSnapshotLoader>()`
// (nullable) without needing to know which provider is configured.
public interface IDispatcherStoreSnapshotLoader
{
    void LoadSnapshot();
}
