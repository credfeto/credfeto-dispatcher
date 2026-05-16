using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace Credfeto.Dispatcher.Storage;

public sealed class ActiveRepoTracker : IActiveRepoTracker
{
    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public ActiveRepoTracker(IDbContextFactory<DispatcherDbContext> dbContextFactory, TimeProvider timeProvider)
    {
        this._dbContextFactory = dbContextFactory;
        this._timeProvider = timeProvider;
    }

    public async ValueTask UpdateActiveReposAsync(
        IReadOnlyList<string> activeRepos,
        CancellationToken cancellationToken
    )
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        List<RepoEntity> existingRepos = await context.Repos.ToListAsync(cancellationToken);

        HashSet<string> pendingNew = new(activeRepos, StringComparer.OrdinalIgnoreCase);

        foreach (RepoEntity existing in existingRepos)
        {
            bool shouldBeActive = pendingNew.Remove(existing.Repository);

            if (existing.IsActive != shouldBeActive)
            {
                existing.IsActive = shouldBeActive;
                existing.LastUpdated = now;
            }
        }

        foreach (string newRepo in pendingNew)
        {
            context.Repos.Add(
                new RepoEntity
                {
                    Repository = newRepo,
                    IsActive = true,
                    LastUpdated = now,
                }
            );
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
