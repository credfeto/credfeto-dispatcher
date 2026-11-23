using System;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Credfeto.Dispatcher.Storage.InMemory;

public static class InMemoryStorageSetup
{
    public static IServiceCollection AddInMemoryStorage(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        return services
            .AddSingleton<InMemoryDispatcherStore>()
            .AddSingleton<IActiveRepoTracker, InMemoryActiveRepoTracker>()
            .AddSingleton<IETagStore, InMemoryETagStore>()
            .AddSingleton<IFeedFreshnessStore, FeedFreshnessStore>()
            .AddSingleton<INotificationStateTracker, InMemoryNotificationStateTracker>()
            .AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>()
            .AddSingleton<DispatcherStoreSnapshotStore>()
            .AddSingleton<IDispatcherStoreSnapshotLoader, DispatcherStoreSnapshotLoader>()
            .AddHostedService<SnapshotWriterService>();
    }
}
