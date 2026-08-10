using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.Storage.InMemory;

[JsonSerializable(typeof(DispatcherStoreSnapshotData))]
internal sealed partial class DispatcherStoreSnapshotSerializerContext : JsonSerializerContext;
