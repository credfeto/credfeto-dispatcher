using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.InMemory;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.InMemory;

public sealed class InMemoryETagStoreTests : TestBase
{
    private const string KEY = "repos/owner/repo/events";

    private readonly IETagStore _eTagStore;

    public InMemoryETagStoreTests()
    {
        this._eTagStore = new InMemoryETagStore(new InMemoryDispatcherStore(MockDateTimeSources.Past));
    }

    [Fact]
    public async Task GetETagAsyncReturnsNullForAnUnknownKeyAsync()
    {
        string? eTag = await this._eTagStore.GetETagAsync(key: KEY, cancellationToken: this.CancellationToken());

        Assert.Null(eTag);
    }

    [Fact]
    public async Task GetETagAsyncReturnsTheLastSavedValueAsync()
    {
        await this._eTagStore.SaveETagAsync(key: KEY, eTag: "\"first\"", cancellationToken: this.CancellationToken());
        await this._eTagStore.SaveETagAsync(key: KEY, eTag: "\"second\"", cancellationToken: this.CancellationToken());

        string? eTag = await this._eTagStore.GetETagAsync(key: KEY, cancellationToken: this.CancellationToken());

        Assert.Equal(expected: "\"second\"", actual: eTag);
    }
}
