using Credfeto.Dispatcher.Storage.InMemory;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests;

public sealed class NullDispatcherStoreSnapshotLoaderTests : DependencyInjectionTestsBase
{
    public NullDispatcherStoreSnapshotLoaderTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddSqlServerStorage();
    }

    [Fact]
    public void SnapshotLoaderShouldBeRegistered()
    {
        this.RequireService<IDispatcherStoreSnapshotLoader>();
    }

    [Fact]
    public void SnapshotLoaderShouldBeOfCorrectType()
    {
        this.RequireServiceInCollectionFor<IDispatcherStoreSnapshotLoader, NullDispatcherStoreSnapshotLoader>();
    }

    [Fact]
    public void LoadSnapshotDoesNotThrow()
    {
        new NullDispatcherStoreSnapshotLoader().LoadSnapshot();
    }
}
