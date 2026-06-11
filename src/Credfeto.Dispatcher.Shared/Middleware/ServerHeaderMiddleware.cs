using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Credfeto.Dispatcher.Shared.Middleware;

public sealed class ServerHeaderMiddleware : IMiddleware
{
    private static readonly string MachineName = Environment.MachineName;

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.Headers["X-Server"] = MachineName;

        return next(context);
    }
}
