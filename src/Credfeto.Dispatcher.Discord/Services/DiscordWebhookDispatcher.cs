using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Discord.Configuration;
using Credfeto.Dispatcher.Discord.DataTypes;
using Credfeto.Dispatcher.Discord.Interfaces;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.Discord.Services;

public sealed class DiscordWebhookDispatcher : IDiscordDispatcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DiscordOptions _options;

    public DiscordWebhookDispatcher(IHttpClientFactory httpClientFactory, IOptions<DiscordOptions> options)
    {
        this._httpClientFactory = httpClientFactory;
        this._options = options.Value;
    }

    public async ValueTask SendAsync(DiscordMessage message, CancellationToken cancellationToken)
    {
        if (this._options.WebhookUrl is null)
        {
            return;
        }

        HttpClient httpClient = this._httpClientFactory.CreateClient("Discord");
        DiscordWebhookPayload payload = BuildPayload(message);

        string json = JsonSerializer.Serialize(value: payload, jsonTypeInfo: DiscordWebhookContext.Default.DiscordWebhookPayload);

        HttpResponseMessage? response = null;

        try
        {
            using HttpRequestMessage request = new(method: HttpMethod.Post, requestUri: this._options.WebhookUrl);
            request.Content = new StringContent(content: json, encoding: Encoding.UTF8, mediaType: "application/json");
            response = await httpClient.SendAsync(request: request, cancellationToken: cancellationToken);
            _ = response.EnsureSuccessStatusCode();
        }
        finally
        {
            response?.Dispose();
        }
    }

    private static DiscordWebhookPayload BuildPayload(DiscordMessage message)
    {
        List<DiscordWebhookEmbed> embeds = new(message.Embeds.Count);

        foreach (DiscordEmbed embed in message.Embeds)
        {
            embeds.Add(new DiscordWebhookEmbed(Title: embed.Title, Description: embed.Description, Url: embed.Url.ToString(), Color: embed.Color));
        }

        return new DiscordWebhookPayload(Content: message.Content, Embeds: embeds);
    }
}
