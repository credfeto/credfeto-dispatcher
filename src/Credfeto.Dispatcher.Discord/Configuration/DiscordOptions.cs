namespace Credfeto.Dispatcher.Discord.Configuration;

public sealed class DiscordOptions
{
    public string WebhookUrl { get; init; } = string.Empty;

    public string NotificationsChannelWebhookUrl { get; init; } = string.Empty;
}
