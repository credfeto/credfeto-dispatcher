using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.Configuration;
using Credfeto.Services.Startup.Interfaces;
using DbUp;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.Storage;

public sealed class DatabaseMigrationService : IRunOnStartup
{
    private readonly IOptions<DatabaseConfiguration> _config;

    public DatabaseMigrationService(IOptions<DatabaseConfiguration> config)
    {
        this._config = config;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken)
    {
        DbUp.Engine.DatabaseUpgradeResult result = DeployChanges
            .To.SqlDatabase(this._config.Value.ConnectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            throw new System.InvalidOperationException(
                $"Database migration failed: {result.Error?.Message}",
                result.Error
            );
        }

        return ValueTask.CompletedTask;
    }
}
