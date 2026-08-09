using System.Collections.Generic;
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

        if (ValidateBoostedRepos(options.Filter.BoostedRepos) is { } boostedReposFailure)
        {
            return boostedReposFailure;
        }

        return ValidateOptionsResult.Success;
    }

    private static ValidateOptionsResult? ValidateBoostedRepos(IReadOnlyList<string> boostedRepos)
    {
        foreach (string entry in boostedRepos)
        {
            string[] parts = entry.Split('/');

            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                return ValidateOptionsResult.Fail(
                    $"GitHub Filter BoostedRepos entry '{entry}' must be in 'owner/repo' format."
                );
            }
        }

        return null;
    }
}
