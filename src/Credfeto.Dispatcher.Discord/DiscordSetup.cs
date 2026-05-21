using System.Net.Http;
using System.Net.Http.Headers;
using Credfeto.Dispatcher.Discord.Configuration;
using Credfeto.Dispatcher.Discord.Interfaces;
using Credfeto.Dispatcher.Discord.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.Discord;

public static class DiscordSetup
{
    private const string USER_AGENT = "credfeto-dispatcher";

    public static IServiceCollection AddDiscord(this IServiceCollection services)
    {
        return services
            .AddSingleton<IValidateOptions<DiscordOptions>, DiscordOptionsValidator>()
            .AddHttpClient(name: "Discord", configureClient: ConfigureDiscordHttpClient)
            .AddStandardResilienceHandler()
            .Services.AddSingleton<IDiscordDispatcher, DiscordWebhookDispatcher>();
    }

    private static void ConfigureDiscordHttpClient(HttpClient client)
    {
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(productName: USER_AGENT, productVersion: null)
        );
    }
}
