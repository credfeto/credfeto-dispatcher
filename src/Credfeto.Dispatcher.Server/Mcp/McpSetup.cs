using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Dispatcher.Server.Mcp;

internal static class McpSetup
{
    public static IServiceCollection AddDispatcherMcp(this IServiceCollection services)
    {
        // Stateless so each POST is self-contained: no session affinity is needed and
        // clients may call tools/list or tools/call without a prior initialize.
        services
            .AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<DispatcherMcpTools>();

        return services;
    }
}
