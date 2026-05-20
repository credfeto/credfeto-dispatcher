using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database.Rows;
using Credfeto.Dispatcher.Storage.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class ETagStoreTests : TestBase
{
    private const string TEST_KEY = "test.key";
    private const string TEST_E_TAG = "\"abc123\"";

    private readonly TestDatabaseStub _database;
    private readonly IETagStore _store;

    public ETagStoreTests()
    {
        this._database = new TestDatabaseStub();
        this._store = new ETagStore(this._database);
    }

    [Fact]
    public async Task GetETagAsyncReturnsNullWhenDatabaseReturnsNullAsync()
    {
        this._database.SetReturn<PollingStateRow?>(value: null);

        string? result = await this._store.GetETagAsync(key: TEST_KEY, cancellationToken: this.CancellationToken());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetETagAsyncReturnsETagFromRowAsync()
    {
        this._database.SetReturn<PollingStateRow?>(new PollingStateRow(Key: TEST_KEY, ETag: TEST_E_TAG));

        string? result = await this._store.GetETagAsync(key: TEST_KEY, cancellationToken: this.CancellationToken());

        Assert.Equal(expected: TEST_E_TAG, actual: result);
    }

    [Fact]
    public async Task SaveETagAsyncCallsDatabaseAsync()
    {
        await this._store.SaveETagAsync(key: TEST_KEY, eTag: TEST_E_TAG, cancellationToken: this.CancellationToken());

        Assert.Equal(expected: 1, actual: this._database.VoidExecuteCallCount);
    }
}
