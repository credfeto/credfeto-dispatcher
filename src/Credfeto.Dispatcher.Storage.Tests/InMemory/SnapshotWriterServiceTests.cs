using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.Configuration;
using Credfeto.Dispatcher.Storage.InMemory;
using FunFair.Test.Common;
using FunFair.Test.Common.Mocks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.InMemory;

// Exercises SnapshotWriterService.WriteIfChangedAsync (the single-tick dirty-check/save-skip
// logic) directly rather than driving BackgroundService's own ExecuteAsync timer loop, against
// a real DispatcherStoreSnapshotStore writing to a real temp directory - simpler and more
// realistic than trying to substitute a sealed, non-virtual store.
public sealed class SnapshotWriterServiceTests : LoggingFolderCleanupTestBase
{
    private const string FILE_NAME = "snapshot.json";

    private readonly InMemoryDispatcherStore _dispatcherStore;
    private readonly DispatcherStoreSnapshotStore _snapshotStore;
    private readonly SnapshotWriterService _service;
    private readonly string _snapshotFilePath;

    public SnapshotWriterServiceTests(ITestOutputHelper output)
        : base(output)
    {
        this._dispatcherStore = new InMemoryDispatcherStore(MockDateTimeSources.Past);

        SnapshotOptions options = new() { DirectoryPath = this.TempFolder, FileName = FILE_NAME };
        this._snapshotStore = new DispatcherStoreSnapshotStore(
            Options.Create(options),
            this.GetTypedLogger<DispatcherStoreSnapshotStore>()
        );
        this._snapshotFilePath = Path.Combine(path1: this.TempFolder, path2: FILE_NAME);

        this._service = new SnapshotWriterService(
            this._dispatcherStore,
            this._snapshotStore,
            Options.Create(options),
            this.GetTypedLogger<SnapshotWriterService>()
        );
    }

    [Fact]
    public async Task FirstTickWritesASnapshotEvenWithNoMutationsAsync()
    {
        await this._service.WriteIfChangedAsync(this.CancellationToken());

        Assert.True(
            condition: File.Exists(this._snapshotFilePath),
            userMessage: "Snapshot file should have been written on the first tick"
        );
    }

    [Fact]
    public async Task SecondTickWithNoChangesDoesNotRewriteTheSnapshotAsync()
    {
        await this._service.WriteIfChangedAsync(this.CancellationToken());
        DateTime firstWriteTime = File.GetLastWriteTimeUtc(this._snapshotFilePath);

        await this._service.WriteIfChangedAsync(this.CancellationToken());
        DateTime secondWriteTime = File.GetLastWriteTimeUtc(this._snapshotFilePath);

        Assert.Equal(expected: firstWriteTime, actual: secondWriteTime);
    }

    [Fact]
    public async Task TickAfterAMutationRewritesTheSnapshotAsync()
    {
        await this._service.WriteIfChangedAsync(this.CancellationToken());

        this._dispatcherStore.SaveETag(key: "key", eTag: "etag");

        await this._service.WriteIfChangedAsync(this.CancellationToken());

        bool loaded = this._snapshotStore.TryLoad(out DispatcherStoreSnapshotData? data);

        Assert.True(condition: loaded, userMessage: "Snapshot written after the mutation should be loadable");
        Assert.NotNull(data);
        Assert.Equal(expected: "etag", actual: Assert.Single(data.PollingStates).Value);
    }
}
