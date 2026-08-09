using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        if (!TryValidateBoostedRepos(options.Filter.BoostedRepos, out string? boostedReposError))
        {
            return ValidateOptionsResult.Fail(boostedReposError);
        }

        return ValidateOptionsResult.Success;
    }

    private static bool TryValidateBoostedRepos(
        IReadOnlyList<string> boostedRepos,
        [NotNullWhen(false)] out string? error
    )
    {
        foreach (string entry in boostedRepos)
        {
            int slash = entry.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);
            int lastSlash = entry.LastIndexOf(value: '/');

            if (slash <= 0 || slash != lastSlash || slash == entry.Length - 1)
            {
                error = $"GitHub Filter BoostedRepos entry '{entry}' must be in 'owner/repo' format.";

                return false;
            }
        }

        error = null;

        return true;
    }
}
