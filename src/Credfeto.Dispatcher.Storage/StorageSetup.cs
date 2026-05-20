using System;
using Credfeto.Database.SqlServer;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Configuration;
using Credfeto.Dispatcher.Storage.Services;
using Credfeto.Services.Startup.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.Storage;

public static class StorageSetup
{
    public static IServiceCollection AddStorage(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IValidateOptions<DatabaseConfiguration>, DatabaseConfigurationValidator>();
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
            .AddSingleton<IPendingNotificationStore, PendingNotificationStore>()
            .AddSingleton<IWorkItemRepository, WorkItemRepository>();
    }
}
