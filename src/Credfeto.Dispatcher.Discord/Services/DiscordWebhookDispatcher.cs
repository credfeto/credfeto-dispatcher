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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DiscordWebhookDispatcher> _logger;
    private readonly DiscordOptions _options;

    public DiscordWebhookDispatcher(
        IHttpClientFactory httpClientFactory,
        IOptions<DiscordOptions> options,
        ILogger<DiscordWebhookDispatcher> logger
    )
    {
        this._httpClientFactory = httpClientFactory;
        this._options = options.Value;
        this._logger = logger;
    }

    public async ValueTask SendAsync(DiscordMessage message, CancellationToken cancellationToken)
    {
        if (this._options.WebhookUrl is null)
        {
            return;
        }

        HttpClient httpClient = this._httpClientFactory.CreateClient("Discord");
        DiscordWebhookPayload payload = BuildPayload(message);

        string json = JsonSerializer.Serialize(
            value: payload,
            jsonTypeInfo: DiscordWebhookContext.Default.DiscordWebhookPayload
        );

        using StringContent content = new(
            content: json,
            encoding: Encoding.UTF8,
            mediaType: "application/json"
        );
        using HttpResponseMessage response = await httpClient.PostAsync(
            requestUri: this._options.WebhookUrl,
            content: content,
            cancellationToken: cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            this._logger.LogWebhookNonSuccess(statusCode: (int)response.StatusCode);
        }

        _ = response.EnsureSuccessStatusCode();
    }

    private static DiscordWebhookPayload BuildPayload(DiscordMessage message)
    {
        List<DiscordWebhookEmbed> embeds = new(message.Embeds.Count);

        foreach (DiscordEmbed embed in message.Embeds)
        {
            IReadOnlyList<DiscordWebhookField>? fields = MapFields(embed.Fields);
            embeds.Add(
                new DiscordWebhookEmbed(
                    Title: embed.Title,
                    Description: embed.Description,
                    Url: embed.Url.ToString(),
                    Color: embed.Color,
                    Fields: fields
                )
            );
        }

        return new DiscordWebhookPayload(Content: message.Content, Embeds: embeds);
    }

    private static IReadOnlyList<DiscordWebhookField>? MapFields(
        IReadOnlyList<Discord.DataTypes.DiscordEmbedField>? fields
    )
    {
        if (fields is null || fields.Count == 0)
        {
            return null;
        }

        DiscordWebhookField[] result = new DiscordWebhookField[fields.Count];

        for (int i = 0; i < fields.Count; i++)
        {
            result[i] = new DiscordWebhookField(
                Name: fields[i].Name,
                Value: fields[i].Value,
                Inline: fields[i].Inline
            );
        }

        return result;
    }
}
