using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.Configuration;

public sealed class GitHubOptionsValidator : IValidateOptions<GitHubOptions>
{
    private const int MinimumPollIntervalSeconds = 30;

    public ValidateOptionsResult Validate(string? name, GitHubOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Token))
        {
            return ValidateOptionsResult.Fail("GitHub API token must be configured.");
        }

        if (options.PollIntervalSeconds < MinimumPollIntervalSeconds)
        {
            return ValidateOptionsResult.Fail($"GitHub PollIntervalSeconds must be at least {MinimumPollIntervalSeconds}.");
        }

        return ValidateOptionsResult.Success;
    }
}
