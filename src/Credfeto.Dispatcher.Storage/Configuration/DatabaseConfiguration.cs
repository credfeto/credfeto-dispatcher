using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Configuration;

[DebuggerDisplay("Provider: {Provider}, ConnectionString: {ConnectionString}")]
public sealed class DatabaseConfiguration
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;

    public string ConnectionString { get; set; } = string.Empty;
}
