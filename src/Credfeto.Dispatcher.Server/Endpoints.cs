using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Credfeto.Dispatcher.Server;

internal static partial class Endpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapPingEndpoints();
        app.MapWorkItemEndpoints();
        app.MapMcp(pattern: "/mcp");
    }
}
