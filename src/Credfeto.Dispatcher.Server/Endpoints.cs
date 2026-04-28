using Microsoft.AspNetCore.Builder;

namespace Credfeto.Dispatcher.Server;

internal static partial class Endpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapWorkItemEndpoints();
    }
}
