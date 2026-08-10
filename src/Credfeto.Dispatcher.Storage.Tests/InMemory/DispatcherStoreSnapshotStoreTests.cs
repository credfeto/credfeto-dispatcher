using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.Configuration;
using Credfeto.Dispatcher.Storage.InMemory;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.InMemory;

public sealed class DispatcherStoreSnapshotStoreTests : LoggingFolderCleanupTestBase
{
    private const string FILE_NAME = "snapshot.json";

    private readonly DispatcherStoreSnapshotStore _store;

    public DispatcherStoreSnapshotStoreTests(ITestOutputHelper output)
        : base(output)
    {
        SnapshotOptions options = new() { DirectoryPath = this.TempFolder, FileName = FILE_NAME };
        this._store = new DispatcherStoreSnapshotStore(
            Options.Create(options),
            this.GetTypedLogger<DispatcherStoreSnapshotStore>()
        );
    }

    private static DispatcherStoreSnapshotData SampleData()
    {
        return new DispatcherStoreSnapshotData(
            Repos: new Dictionary<string, bool>(StringComparer.Ordinal) { ["owner/repo"] = true },
            PullRequests: [],
            Issues: [],
            PollingStates: new Dictionary<string, string>(StringComparer.Ordinal) { ["poll-key"] = "\"abc123\"" }
        );
    }

    [Fact]
    public void TryLoadReturnsFalseWhenNoFileHasEverBeenSaved()
    {
        bool result = this._store.TryLoad(out DispatcherStoreSnapshotData? data);

        Assert.False(
            condition: result,
            userMessage: "TryLoad should report no data when no snapshot has ever been saved"
        );
        Assert.Null(data);
    }

    [Fact]
    public async Task SaveAsyncThenTryLoadRoundTripsTheDataAsync()
    {
        DispatcherStoreSnapshotData original = SampleData();

        await this._store.SaveAsync(data: original, cancellationToken: this.CancellationToken());

        bool result = this._store.TryLoad(out DispatcherStoreSnapshotData? loaded);

        Assert.True(condition: result, userMessage: "TryLoad should report data after a successful save");
        Assert.NotNull(loaded);
        KeyValuePair<string, bool> repo = Assert.Single(loaded.Repos);
        Assert.Equal(expected: "owner/repo", actual: repo.Key);
        Assert.True(condition: repo.Value, userMessage: "Repo should have round-tripped as active");
        KeyValuePair<string, string> pollingState = Assert.Single(loaded.PollingStates);
        Assert.Equal(expected: "poll-key", actual: pollingState.Key);
        Assert.Equal(expected: "\"abc123\"", actual: pollingState.Value);
    }

    [Fact]
    public async Task SaveAsyncLeavesNoTemporaryFileBehindOnSuccessAsync()
    {
        await this._store.SaveAsync(data: SampleData(), cancellationToken: this.CancellationToken());

        string[] filesInDirectory = Directory.GetFiles(this.TempFolder);
        string onlyFile = Assert.Single(filesInDirectory);
        Assert.Equal(expected: FILE_NAME, actual: Path.GetFileName(onlyFile));
    }

    [Fact]
    public async Task TryLoadReturnsFalseAndDoesNotThrowWhenTheFileIsCorruptAsync()
    {
        string filePath = Path.Combine(path1: this.TempFolder, path2: FILE_NAME);
        await File.WriteAllTextAsync(
            path: filePath,
            contents: "{ not valid json",
            cancellationToken: this.CancellationToken()
        );

        bool result = this._store.TryLoad(out DispatcherStoreSnapshotData? data);

        Assert.False(condition: result, userMessage: "TryLoad should not throw or report success for a corrupt file");
        Assert.Null(data);
    }

    [Fact]
    public async Task SaveAsyncOverwritesAPreviouslySavedSnapshotAsync()
    {
        await this._store.SaveAsync(data: SampleData(), cancellationToken: this.CancellationToken());

        DispatcherStoreSnapshotData updated = new(
            Repos: [],
            PullRequests: [],
            Issues: [],
            PollingStates: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["different-key"] = "different-etag",
            }
        );
        await this._store.SaveAsync(data: updated, cancellationToken: this.CancellationToken());

        this._store.TryLoad(out DispatcherStoreSnapshotData? loaded);

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Repos);
        KeyValuePair<string, string> pollingState = Assert.Single(loaded.PollingStates);
        Assert.Equal(expected: "different-key", actual: pollingState.Key);
    }

    [Fact]
    public async Task SaveAsyncCreatesTheDirectoryWhenItDoesNotExistYetAsync()
    {
        string nestedDirectory = Path.Combine(path1: this.TempFolder, path2: "nested");
        SnapshotOptions options = new() { DirectoryPath = nestedDirectory, FileName = FILE_NAME };
        DispatcherStoreSnapshotStore store = new(
            Options.Create(options),
            this.GetTypedLogger<DispatcherStoreSnapshotStore>()
        );

        await store.SaveAsync(data: SampleData(), cancellationToken: this.CancellationToken());

        Assert.True(
            condition: File.Exists(Path.Combine(path1: nestedDirectory, path2: FILE_NAME)),
            userMessage: "SaveAsync should create the directory when it does not already exist"
        );
    }
}
