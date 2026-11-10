using System.Collections.Generic;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.InMemory;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.InMemory;

public sealed class InMemoryActiveRepoTrackerTests : TestBase
{
    private readonly IActiveRepoTracker _tracker;

    public InMemoryActiveRepoTrackerTests()
    {
        this._tracker = new InMemoryActiveRepoTracker(new InMemoryDispatcherStore(MockDateTimeSources.Past));
    }

    [Fact]
    public async Task GetActiveReposAsyncReturnsNothingBeforeAnyUpdateAsync()
    {
        IReadOnlyList<string> repos = await this._tracker.GetActiveReposAsync(this.CancellationToken());

        Assert.Empty(repos);
    }

    [Fact]
    public async Task GetActiveReposAsyncReturnsTheReposLastMarkedActiveAsync()
    {
        await this._tracker.UpdateActiveReposAsync(
            activeRepos: ["owner/one", "owner/two"],
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<string> repos = await this._tracker.GetActiveReposAsync(this.CancellationToken());

        Assert.Equal(expected: ["owner/one", "owner/two"], actual: [.. repos]);
    }
}
