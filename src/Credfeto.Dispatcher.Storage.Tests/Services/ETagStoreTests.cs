using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Services;
using FunFair.Test.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class ETagStoreTests : TestBase, IAsyncLifetime
{
    private const string TestKey = "test.key";
    private const string TestETag = "\"abc123\"";
    private const string UpdatedETag = "\"xyz789\"";

    private readonly IETagStore _store;
    private readonly SqliteConnection _connection;

    public ETagStoreTests()
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

        this._store = new ETagStore(new TestDbContextFactory(options));
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return this._connection.DisposeAsync();
    }

    [Fact]
    public async Task GetETagAsyncReturnsNullWhenNoRecordExistsAsync()
    {
        string? result = await this._store.GetETagAsync(key: TestKey, cancellationToken: this.CancellationToken());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetETagAsyncReturnsStoredValueAfterSaveAsync()
    {
        await this._store.SaveETagAsync(key: TestKey, eTag: TestETag, cancellationToken: this.CancellationToken());

        string? result = await this._store.GetETagAsync(key: TestKey, cancellationToken: this.CancellationToken());

        Assert.Equal(expected: TestETag, actual: result);
    }

    [Fact]
    public async Task SaveETagAsyncCreatesNewRecordWhenNoneExistsAsync()
    {
        await this._store.SaveETagAsync(key: TestKey, eTag: TestETag, cancellationToken: this.CancellationToken());

        string? result = await this._store.GetETagAsync(key: TestKey, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SaveETagAsyncUpdatesExistingRecordAsync()
    {
        await this._store.SaveETagAsync(key: TestKey, eTag: TestETag, cancellationToken: this.CancellationToken());
        await this._store.SaveETagAsync(key: TestKey, eTag: UpdatedETag, cancellationToken: this.CancellationToken());

        string? result = await this._store.GetETagAsync(key: TestKey, cancellationToken: this.CancellationToken());

        Assert.Equal(expected: UpdatedETag, actual: result);
    }

    [Fact]
    public async Task SaveETagAsyncDoesNotAffectOtherKeysAsync()
    {
        const string otherKey = "other.key";

        await this._store.SaveETagAsync(key: TestKey, eTag: TestETag, cancellationToken: this.CancellationToken());

        string? result = await this._store.GetETagAsync(key: otherKey, cancellationToken: this.CancellationToken());

        Assert.Null(result);
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
