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
    private readonly HttpClient _httpClient;
    private readonly DiscordOptions _options;

    public DiscordWebhookDispatcher(HttpClient httpClient, IOptions<DiscordOptions> options)
    {
        this._httpClient = httpClient;
        this._options = options.Value;
    }

    public async ValueTask SendAsync(DiscordMessage message, CancellationToken cancellationToken)
    {
        if (this._options.WebhookUrl is null)
        {
            return;
        }

        DiscordWebhookPayload payload = BuildPayload(message);

        string json = JsonSerializer.Serialize(value: payload, jsonTypeInfo: DiscordWebhookContext.Default.DiscordWebhookPayload);

        using HttpRequestMessage request = new(method: HttpMethod.Post, requestUri: this._options.WebhookUrl);
        request.Content = new StringContent(content: json, encoding: Encoding.UTF8, mediaType: "application/json");

        using HttpResponseMessage response = await this._httpClient.SendAsync(request: request, cancellationToken: cancellationToken);

        response.EnsureSuccessStatusCode();
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
