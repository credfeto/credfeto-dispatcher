using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace Credfeto.Dispatcher.Storage.Services;

public sealed class ETagStore : IETagStore
{
    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;

    public ETagStore(IDbContextFactory<DispatcherDbContext> dbContextFactory)
    {
        this._dbContextFactory = dbContextFactory;
    }

    public async ValueTask<string?> GetETagAsync(string key, CancellationToken cancellationToken)
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        PollingStateEntity? entity = await context.PollingStates.FindAsync(keyValues: [key], cancellationToken: cancellationToken);

        return entity?.ETag;
    }

    public async ValueTask SaveETagAsync(string key, string eTag, CancellationToken cancellationToken)
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        PollingStateEntity? existing = await context.PollingStates.FindAsync(keyValues: [key], cancellationToken: cancellationToken);

        if (existing is null)
        {
            context.PollingStates.Add(new PollingStateEntity { Key = key, ETag = eTag });
        }
        else
        {
            context.PollingStates.Entry(existing).CurrentValues.SetValues(new PollingStateEntity { Key = key, ETag = eTag });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
