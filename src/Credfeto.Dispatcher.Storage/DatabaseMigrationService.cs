using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Credfeto.Dispatcher.Storage;

public sealed class DatabaseMigrationService : IHostedService
{
    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;

    public DatabaseMigrationService(IDbContextFactory<DispatcherDbContext> dbContextFactory)
    {
        this._dbContextFactory = dbContextFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
