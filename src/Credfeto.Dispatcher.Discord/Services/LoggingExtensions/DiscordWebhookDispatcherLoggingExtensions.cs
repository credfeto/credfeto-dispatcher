using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.Discord.Services.LoggingExtensions;

internal static partial class DiscordWebhookDispatcherLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Discord webhook returned non-success status: {StatusCode}")]
    public static partial void LogWebhookNonSuccess(this ILogger logger, int statusCode);
}
