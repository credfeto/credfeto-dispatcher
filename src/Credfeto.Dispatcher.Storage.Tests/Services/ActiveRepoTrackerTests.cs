using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Entities;
using FunFair.Test.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class ActiveRepoTrackerTests : TestBase, IAsyncLifetime
{
    private static readonly DateTimeOffset BaseTime = new(
        year: 2025,
        month: 1,
        day: 1,
        hour: 0,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    private readonly SqliteConnection _connection;
    private readonly DispatcherDbContext _readContext;
    private readonly IActiveRepoTracker _tracker;

    public ActiveRepoTrackerTests()
    {
        this._connection = new SqliteConnection("DataSource=:memory:");
        this._connection.Open();

        DbContextOptions<DispatcherDbContext> options = new DbContextOptionsBuilder<DispatcherDbContext>()
            .UseSqlite(this._connection)
            .Options;

        using (DispatcherDbContext ctx = new(options))
        {
            ctx.Database.Migrate();
        }

        TestDbContextFactory factory = new(options);
        this._readContext = new DispatcherDbContext(options);
        FakeTimeProvider timeProvider = new(BaseTime);
        this._tracker = new ActiveRepoTracker(factory, timeProvider);
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await this._readContext.DisposeAsync();
        await this._connection.DisposeAsync();
    }

    [Fact]
    public async Task NewActiveReposAreAddedAsActiveAsync()
    {
        await this._tracker.UpdateActiveReposAsync(
            activeRepos: ["owner/repo-a", "owner/repo-b"],
            cancellationToken: this.CancellationToken()
        );

        List<RepoEntity> repos = await this._readContext.Repos.ToListAsync(this.CancellationToken());

        Assert.Equal(expected: 2, actual: repos.Count);
        Assert.All(repos, r => Assert.True(r.IsActive, userMessage: $"Repo {r.Repository} should be active"));
    }

    [Fact]
    public async Task RepoRemovedFromActiveScanIsMarkedInactiveAsync()
    {
        await this._tracker.UpdateActiveReposAsync(
            activeRepos: ["owner/repo-a", "owner/repo-b"],
            cancellationToken: this.CancellationToken()
        );

        await this._tracker.UpdateActiveReposAsync(
            activeRepos: ["owner/repo-a"],
            cancellationToken: this.CancellationToken()
        );

        RepoEntity? repoB = await this._readContext.Repos.FindAsync(["owner/repo-b"], this.CancellationToken());

        Assert.NotNull(repoB);
        Assert.False(repoB.IsActive, userMessage: "owner/repo-b should be marked inactive after scan excluded it");
    }

    [Fact]
    public async Task RepoReturnedToActiveScanIsMarkedActiveAsync()
    {
        await this._tracker.UpdateActiveReposAsync(
            activeRepos: ["owner/repo-a"],
            cancellationToken: this.CancellationToken()
        );

        await this._tracker.UpdateActiveReposAsync(activeRepos: [], cancellationToken: this.CancellationToken());

        await this._tracker.UpdateActiveReposAsync(
            activeRepos: ["owner/repo-a"],
            cancellationToken: this.CancellationToken()
        );

        RepoEntity? repo = await this._readContext.Repos.FindAsync(["owner/repo-a"], this.CancellationToken());

        Assert.NotNull(repo);
        Assert.True(repo.IsActive, userMessage: "owner/repo-a should be active after being re-added to the scan");
    }

    [Fact]
    public async Task EmptyActiveScanMarksAllReposInactiveAsync()
    {
        await this._tracker.UpdateActiveReposAsync(
            activeRepos: ["owner/repo-a", "owner/repo-b"],
            cancellationToken: this.CancellationToken()
        );

        await this._tracker.UpdateActiveReposAsync(activeRepos: [], cancellationToken: this.CancellationToken());

        List<RepoEntity> repos = await this._readContext.Repos.ToListAsync(this.CancellationToken());

        Assert.Equal(expected: 2, actual: repos.Count);
        Assert.All(
            repos,
            r => Assert.False(r.IsActive, userMessage: $"Repo {r.Repository} should be inactive after empty scan")
        );
    }

    private sealed class TestDbContextFactory : IDbContextFactory<DispatcherDbContext>
    {
        private readonly DbContextOptions<DispatcherDbContext> _options;

        public TestDbContextFactory(DbContextOptions<DispatcherDbContext> options)
        {
            this._options = options;
        }

        public DispatcherDbContext CreateDbContext()
        {
            return new DispatcherDbContext(this._options);
        }

        public Task<DispatcherDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DispatcherDbContext(this._options));
        }
    }
}
