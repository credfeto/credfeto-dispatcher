using Credfeto.Dispatcher.Storage.InMemory;

namespace Credfeto.Dispatcher.Storage;

// Registered under DatabaseProvider.SqlServer, where there is no in-memory store to load a
// snapshot into - see the registration in StorageSetup.AddSqlServerStorage. Lets ServerStartup
// resolve IDispatcherStoreSnapshotLoader via GetRequiredService regardless of which provider is
// configured, rather than needing a nullable GetService call.
public sealed class NullDispatcherStoreSnapshotLoader : IDispatcherStoreSnapshotLoader
{
    public void LoadSnapshot() { }
}
