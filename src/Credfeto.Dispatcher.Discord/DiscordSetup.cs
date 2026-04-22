using System.Net.Http;
using System.Net.Http.Headers;
using Credfeto.Dispatcher.Discord.Interfaces;
using Credfeto.Dispatcher.Discord.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Dispatcher.Discord;

public static class DiscordSetup
{
    private const string UserAgent = "credfeto-dispatcher";

    public static IServiceCollection AddDiscord(this IServiceCollection services)
    {
        return services
            .AddHttpClient<IDiscordDispatcher, DiscordWebhookDispatcher>(ConfigureDiscordHttpClient)
            .Services;
    }

    private static void ConfigureDiscordHttpClient(HttpClient client)
    {
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(productName: UserAgent, productVersion: null));
    }
}
