using Credfeto.Dispatcher.Shared.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Dispatcher.Shared;

public static class SharedSetup
{
    public static IServiceCollection AddResources(this IServiceCollection services)
    {
        return services.AddSingleton<ServerHeaderMiddleware>();
    }
}
