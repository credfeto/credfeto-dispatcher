using System.IO;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.Configuration;
using Credfeto.Dispatcher.Storage.InMemory;
using FunFair.Test.Common;
using FunFair.Test.Common.Mocks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.InMemory;

// The one component sitting on the pre-Kestrel startup path (see ServerStartup.CreateApp) -
// covers exactly the tolerance guarantees the store's own tests don't: LoadSnapshot must never
// throw regardless of what it finds on disk.
public sealed class DispatcherStoreSnapshotLoaderTests : LoggingFolderCleanupTestBase
{
    private const string FILE_NAME = "snapshot.json";

    private readonly InMemoryDispatcherStore _dispatcherStore;
    private readonly IDispatcherStoreSnapshotLoader _loader;
    private readonly string _filePath;

    public DispatcherStoreSnapshotLoaderTests(ITestOutputHelper output)
        : base(output)
    {
        this._dispatcherStore = new InMemoryDispatcherStore(MockDateTimeSources.Past);

        SnapshotOptions options = new() { DirectoryPath = this.TempFolder, FileName = FILE_NAME };
        DispatcherStoreSnapshotStore snapshotStore = new(
            Options.Create(options),
            this.GetTypedLogger<DispatcherStoreSnapshotStore>()
        );
        this._filePath = Path.Combine(path1: this.TempFolder, path2: FILE_NAME);

        this._loader = new DispatcherStoreSnapshotLoader(
            this._dispatcherStore,
            snapshotStore,
            this.GetTypedLogger<DispatcherStoreSnapshotLoader>()
        );
    }

    [Fact]
    public void LoadSnapshotDoesNotThrowAndLeavesAnEmptyStoreWhenNoFileExists()
    {
        this._loader.LoadSnapshot();

        Assert.Empty(this._dispatcherStore.GetActiveRepos());
    }

    [Fact]
    public async Task LoadSnapshotDoesNotThrowAndLeavesAnEmptyStoreWhenTheFileIsCorruptAsync()
    {
        await File.WriteAllTextAsync(
            path: this._filePath,
            contents: "{ not valid json",
            cancellationToken: this.CancellationToken()
        );

        this._loader.LoadSnapshot();

        Assert.Empty(this._dispatcherStore.GetActiveRepos());
    }

    [Fact]
    public async Task LoadSnapshotDoesNotThrowAndLeavesAnEmptyStoreWhenTheFileIsStructurallyValidButMissingFieldsAsync()
    {
        // "{}" deserializes successfully (System.Text.Json does not enforce non-nullable
        // reference types at runtime) with all four array properties left null - the exact case
        // that would otherwise NullReferenceException inside ImportSnapshot's first foreach.
        await File.WriteAllTextAsync(path: this._filePath, contents: "{}", cancellationToken: this.CancellationToken());

        this._loader.LoadSnapshot();

        Assert.Empty(this._dispatcherStore.GetActiveRepos());
    }

    [Fact]
    public async Task LoadSnapshotImportsAValidSnapshotIntoTheStoreAsync()
    {
        const string json = """
            {"Repos":[{"Repository":"owner/repo","IsActive":true}],"PullRequests":[],"Issues":[],"PollingStates":[]}
            """;
        await File.WriteAllTextAsync(path: this._filePath, contents: json, cancellationToken: this.CancellationToken());

        this._loader.LoadSnapshot();

        Assert.Equal(expected: "owner/repo", actual: Assert.Single(this._dispatcherStore.GetActiveRepos()));
    }
}
