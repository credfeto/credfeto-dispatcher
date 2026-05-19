using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Configuration;

[DebuggerDisplay("ConnectionString: {ConnectionString}")]
public sealed class DatabaseConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
}
