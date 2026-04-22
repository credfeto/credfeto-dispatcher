using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Discord.Configuration;
using Credfeto.Dispatcher.Discord.DataTypes;
using Credfeto.Dispatcher.Discord.Interfaces;
using Credfeto.Dispatcher.Discord.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.Discord.Services;

public sealed class DiscordWebhookDispatcher : IDiscordDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DiscordWebhookDispatcher> _logger;
    private readonly DiscordOptions _options;

    public DiscordWebhookDispatcher(HttpClient httpClient, IOptions<DiscordOptions> options, ILogger<DiscordWebhookDispatcher> logger)
    {
        this._httpClient = httpClient;
        this._options = options.Value;
        this._logger = logger;
    }

    public async ValueTask SendAsync(DiscordMessage message, CancellationToken cancellationToken)
    {
        if (this._options.WebhookUrl is null)
        {
            return;
        }

        DiscordWebhookPayload payload = BuildPayload(message);

        string json = JsonSerializer.Serialize(value: payload, jsonTypeInfo: DiscordWebhookContext.Default.DiscordWebhookPayload);

        HttpResponseMessage? response = null;

        try
        {
            using HttpRequestMessage request = new(method: HttpMethod.Post, requestUri: this._options.WebhookUrl);
            request.Content = new StringContent(content: json, encoding: Encoding.UTF8, mediaType: "application/json");
            response = await this._httpClient.SendAsync(request: request, cancellationToken: cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                this._logger.LogWebhookNonSuccess(statusCode: (int)response.StatusCode);
            }

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
