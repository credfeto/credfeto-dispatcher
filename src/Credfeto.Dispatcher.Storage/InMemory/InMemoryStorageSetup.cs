using System;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            .AddSingleton<INotificationStateTracker, InMemoryNotificationStateTracker>()
            .AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>();
    }
}
