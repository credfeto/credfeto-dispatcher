using System;
using System.Linq;
using Credfeto.Database.SqlServer;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Configuration;
using Credfeto.Dispatcher.Storage.InMemory;
using Credfeto.Dispatcher.Storage.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests;

public sealed class StorageSetupTests : TestBase
{
    private const string CONNECTION_STRING = "Server=localhost;Database=Dispatcher";

    [Fact]
    public void AddStorageWithInMemoryProviderRegistersTheInMemoryImplementations()
    {
        IServiceCollection services = new ServiceCollection().AddStorage(
            new DatabaseConfiguration { Provider = DatabaseProvider.InMemory }
        );

        AssertRegistered<IValidateOptions<DatabaseConfiguration>, DatabaseConfigurationValidator>(services);
        AssertRegistered<IActiveRepoTracker, InMemoryActiveRepoTracker>(services);
        AssertRegistered<IETagStore, InMemoryETagStore>(services);
        AssertRegistered<INotificationStateTracker, InMemoryNotificationStateTracker>(services);
        AssertRegistered<IWorkItemRepository, InMemoryWorkItemRepository>(services);
    }

    [Fact]
    public void AddStorageWithSqlServerProviderRegistersTheSqlServerImplementations()
    {
        IServiceCollection services = new ServiceCollection().AddStorage(
            new DatabaseConfiguration { Provider = DatabaseProvider.SqlServer, ConnectionString = CONNECTION_STRING }
        );

        AssertRegistered<IValidateOptions<DatabaseConfiguration>, DatabaseConfigurationValidator>(services);
        AssertRegistered<IActiveRepoTracker, ActiveRepoTracker>(services);
        AssertRegistered<IETagStore, ETagStore>(services);
        AssertRegistered<INotificationStateTracker, NotificationStateTracker>(services);
        AssertRegistered<IWorkItemRepository, WorkItemRepository>(services);
        AssertRegistered<IDispatcherStoreSnapshotLoader, NullDispatcherStoreSnapshotLoader>(services);
    }

    [Fact]
    public void AddStorageWithUnknownProviderThrows()
    {
        DatabaseConfiguration configuration = new() { Provider = (DatabaseProvider)99 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddStorage(configuration));
    }

    [Fact]
    public void AddSqlServerStorageTakesTheSqlServerConnectionStringFromTheDatabaseConfiguration()
    {
        ServiceCollection services = new();
        services.AddSingleton(Options.Create(new DatabaseConfiguration { ConnectionString = CONNECTION_STRING }));
        services.AddSqlServerStorage();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        SqlServerConfiguration sqlServerConfiguration = serviceProvider
            .GetRequiredService<IOptions<SqlServerConfiguration>>()
            .Value;

        Assert.Equal(expected: CONNECTION_STRING, actual: sqlServerConfiguration.ConnectionString);
    }

    private static void AssertRegistered<TService, TImplementation>(IServiceCollection services)
        where TImplementation : TService
    {
        Assert.Contains(
            collection: services.Where(descriptor => descriptor.ServiceType == typeof(TService)),
            filter: descriptor => descriptor.ImplementationType == typeof(TImplementation)
        );
    }
}
