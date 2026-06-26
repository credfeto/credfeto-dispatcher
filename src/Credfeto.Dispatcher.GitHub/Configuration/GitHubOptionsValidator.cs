using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.Configuration;

public sealed class GitHubOptionsValidator : IValidateOptions<GitHubOptions>
{
    private const int MINIMUM_POLL_INTERVAL_SECONDS = 30;

    public ValidateOptionsResult Validate(string? name, GitHubOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Token))
        {
            return ValidateOptionsResult.Fail("GitHub API token must be configured.");
        }

        if (options.ApiBaseUrl is null)
        {
            return ValidateOptionsResult.Fail("GitHub ApiBaseUrl must be configured.");
        }

        if (options.PollIntervalSeconds < MINIMUM_POLL_INTERVAL_SECONDS)
        {
            return ValidateOptionsResult.Fail(
                $"GitHub PollIntervalSeconds must be at least {MINIMUM_POLL_INTERVAL_SECONDS}."
            );
        }

        return ValidateOptionsResult.Success;
    }
}
