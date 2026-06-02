using System;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Shared.Middleware;
using FunFair.Test.Common;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Credfeto.Dispatcher.Shared.Tests.Middleware;

public sealed class ServerHeaderMiddlewareTests : TestBase
{
    [Fact]
    public async Task XServerHeaderShouldBeSetToMachineNameAsync()
    {
        DefaultHttpContext context = new();
        ServerHeaderMiddleware middleware = new();

        await middleware.InvokeAsync(context, static _ => Task.CompletedTask);

        Assert.Equal(expected: Environment.MachineName, actual: (string?)context.Response.Headers["X-Server"]);
    }

    [Fact]
    public async Task NextMiddlewareShouldBeCalledAsync()
    {
        DefaultHttpContext context = new();
        bool nextCalled = false;
        ServerHeaderMiddleware middleware = new();

        await middleware.InvokeAsync(
            context,
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );

        Assert.True(nextCalled, "next should be called");
    }
}
