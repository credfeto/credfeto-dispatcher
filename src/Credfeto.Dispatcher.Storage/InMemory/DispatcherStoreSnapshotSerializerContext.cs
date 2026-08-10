using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.Storage.InMemory;

[JsonSerializable(typeof(DispatcherStoreSnapshotData))]
[JsonSerializable(typeof(Dictionary<string, bool>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class DispatcherStoreSnapshotSerializerContext : JsonSerializerContext;
