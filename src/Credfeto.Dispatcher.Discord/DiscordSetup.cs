using Credfeto.Dispatcher.Discord.Interfaces;
using Credfeto.Dispatcher.Discord.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Dispatcher.Discord;

public static class DiscordSetup
{
    public static IServiceCollection AddDiscord(this IServiceCollection services)
    {
        return services
            .AddHttpClient<IDiscordDispatcher, DiscordWebhookDispatcher>()
            .Services;
    }
}
