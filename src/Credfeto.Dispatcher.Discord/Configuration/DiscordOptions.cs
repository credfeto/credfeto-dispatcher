using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Discord.Configuration;

[DebuggerDisplay("WebhookUrl: {WebhookUrl}")]
public sealed class DiscordOptions
{
    public Uri? WebhookUrl { get; set; }

    public Uri? NotificationsChannelWebhookUrl { get; set; }
}
