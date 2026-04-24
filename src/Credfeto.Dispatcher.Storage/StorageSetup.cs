using System.IO;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Services;
using Credfeto.Services.Startup.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Credfeto.Dispatcher.Storage;

public static class StorageSetup
{
    private const string DataFolder = "data";
    private const string DatabaseFileName = "dispatcher.db";

    public static IServiceCollection AddStorage(this IServiceCollection services, IHostEnvironment environment)
    {
        string dataPath = Path.Combine(environment.ContentRootPath, DataFolder);
        Directory.CreateDirectory(dataPath);

        string dbPath = Path.Combine(dataPath, DatabaseFileName);

        return services
            .AddDbContextFactory<DispatcherDbContext>(options => options.UseSqlite($"Data Source={dbPath}"))
            .AddRunOnStartupTask<DatabaseMigrationService>()
            .AddSingleton<IETagStore, ETagStore>()
            .AddSingleton<INotificationStateTracker, NotificationStateTracker>()
            .AddSingleton<INotificationQueue, NotificationQueue>();
    }
}
