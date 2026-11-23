using System;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.InMemory;
using Credfeto.Dispatcher.Storage.Services;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class FeedFreshnessStoreTests : TestBase
{
    private static readonly DateTimeOffset BaseTime = new(
        year: 2026,
        month: 11,
        day: 23,
        hour: 12,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    private readonly InMemoryETagStore _eTagStore;
    private readonly FeedFreshnessStore _store;

    public FeedFreshnessStoreTests()
    {
        this._eTagStore = new InMemoryETagStore(new InMemoryDispatcherStore(MockDateTimeSources.Past));
        this._store = new FeedFreshnessStore(this._eTagStore);
    }

    [Fact]
    public async Task GetLastSyncAsyncWithNothingRecordedReturnsNullAsync()
    {
        Assert.Null(await this._store.GetLastSyncAsync(this.CancellationToken()));
    }

    [Fact]
    public async Task RecordSyncAsyncRoundTripsTheTimeAsync()
    {
        await this._store.RecordSyncAsync(syncedAt: BaseTime, cancellationToken: this.CancellationToken());

        Assert.Equal(expected: BaseTime, actual: await this._store.GetLastSyncAsync(this.CancellationToken()));
    }

    [Fact]
    public async Task RecordSyncAsyncKeepsTheNewerTimeAsync()
    {
        await this._store.RecordSyncAsync(syncedAt: BaseTime, cancellationToken: this.CancellationToken());
        await this._store.RecordSyncAsync(
            syncedAt: BaseTime.AddMinutes(-30),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: BaseTime, actual: await this._store.GetLastSyncAsync(this.CancellationToken()));

        await this._store.RecordSyncAsync(
            syncedAt: BaseTime.AddMinutes(1),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(
            expected: BaseTime.AddMinutes(1),
            actual: await this._store.GetLastSyncAsync(this.CancellationToken())
        );
    }

    [Fact]
    public async Task GetLastSyncAsyncIgnoresAnUnreadableValueAsync()
    {
        await this._eTagStore.SaveETagAsync(
            key: FeedFreshnessStore.LAST_SYNC_KEY,
            eTag: "not a time",
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(await this._store.GetLastSyncAsync(this.CancellationToken()));
    }
}
