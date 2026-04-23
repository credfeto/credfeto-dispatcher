using Credfeto.Dispatcher.Discord.Configuration;
using Credfeto.Dispatcher.Discord.Interfaces;
using Credfeto.Dispatcher.Discord.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.Discord.Tests;

public sealed class DiscordSetupTests : DependencyInjectionTestsBase
{
    public DiscordSetupTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure)
    {
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddDiscord()
                       .AddMockedService<IOptions<DiscordOptions>>(static o => o.Value.Returns(new DiscordOptions()));
    }

    [Fact]
    public void DiscordDispatcherShouldBeRegistered()
    {
        this.RequireService<IDiscordDispatcher>();
    }

    [Fact]
    public void DiscordDispatcherShouldBeOfCorrectType()
    {
        this.RequireServiceInCollectionFor<IDiscordDispatcher, DiscordWebhookDispatcher>();
    }
}
