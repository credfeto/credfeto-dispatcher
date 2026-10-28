using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.Storage;

[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(ImmutableArray<string>))]
internal sealed partial class CheckNamesSerializerContext : JsonSerializerContext;
