using System.Threading;
using System.Threading.Tasks;
using Credfeto.Services.Startup.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Credfeto.Dispatcher.Storage;

public sealed class DatabaseMigrationService : IRunOnStartup
{
    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;

    public DatabaseMigrationService(IDbContextFactory<DispatcherDbContext> dbContextFactory)
    {
        this._dbContextFactory = dbContextFactory;
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }
}
