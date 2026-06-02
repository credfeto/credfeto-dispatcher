using Credfeto.Dispatcher.Shared.Middleware;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.Dispatcher.Shared.Tests;

public sealed class SharedSetupTests : DependencyInjectionTestsBase
{
    public SharedSetupTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddResources();
    }

    [Fact]
    public void ServerHeaderMiddlewareShouldBeRegistered()
    {
        this.RequireService<ServerHeaderMiddleware>();
    }
}
