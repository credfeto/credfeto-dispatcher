using System;
using Credfeto.Database.SqlServer;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Configuration;
using Credfeto.Dispatcher.Storage.InMemory;
using Credfeto.Dispatcher.Storage.Services;
using Credfeto.Services.Startup.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.Storage;

public static class StorageSetup
{
    public static IServiceCollection AddStorage(this IServiceCollection services, DatabaseConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<DatabaseConfiguration>, DatabaseConfigurationValidator>();

        return configuration.Provider switch
        {
            DatabaseProvider.InMemory => services.AddInMemoryStorage(),
            DatabaseProvider.SqlServer => services.AddSqlServerStorage(),
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(configuration),
                actualValue: configuration.Provider,
                message: "Unsupported database provider."
            ),
        };
    }

    public static IServiceCollection AddSqlServerStorage(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IOptions<SqlServerConfiguration>>(sp =>
        {
            DatabaseConfiguration cfg = sp.GetRequiredService<IOptions<DatabaseConfiguration>>().Value;
            return Options.Create(new SqlServerConfiguration(cfg.ConnectionString));
        });

        return services
            .AddSqlServer()
            .AddRunOnStartupTask<DatabaseMigrationService>()
            .AddSingleton<IActiveRepoTracker, ActiveRepoTracker>()
            .AddSingleton<IETagStore, ETagStore>()
            .AddSingleton<INotificationStateTracker, NotificationStateTracker>()
            .AddSingleton<IWorkItemRepository, WorkItemRepository>();
    }
}
