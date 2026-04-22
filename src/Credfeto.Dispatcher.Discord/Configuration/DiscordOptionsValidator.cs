using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.Discord.Configuration;

public sealed class DiscordOptionsValidator : IValidateOptions<DiscordOptions>
{
    public ValidateOptionsResult Validate(string? name, DiscordOptions options)
    {
        if (options.WebhookUrl is null)
        {
            return ValidateOptionsResult.Fail("Discord WebhookUrl must be configured.");
        }

        return ValidateOptionsResult.Success;
    }
}
