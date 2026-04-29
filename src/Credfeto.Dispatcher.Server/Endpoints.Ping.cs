using Credfeto.Dispatcher.GitHub.DataTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Credfeto.Dispatcher.Server;

internal static partial class Endpoints
{
    private static readonly PongDto PongModel = new("Pong!");

    public static void MapPingEndpoints(this WebApplication app)
    {
        app.MapGet(pattern: "/ping", handler: static () => Results.Ok(PongModel));
    }
}
