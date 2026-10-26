using System;
using System.Text.RegularExpressions;

namespace Credfeto.Dispatcher.GitHub;

public static partial class GitHubRepositoryNameValidator
{
    private const int MAX_REPOSITORY_NAME_LENGTH = 100;

    public static bool IsValid(string name)
    {
        return name.Length <= MAX_REPOSITORY_NAME_LENGTH && !IsDotOnly(name) && RepositoryNamePattern().IsMatch(name);
    }

    // GitHub reserves "." and ".." as repository names.
    private static bool IsDotOnly(string name)
    {
        return string.Equals(a: name, b: ".", comparisonType: StringComparison.Ordinal)
            || string.Equals(a: name, b: "..", comparisonType: StringComparison.Ordinal);
    }

    [GeneratedRegex(
        pattern: "^[A-Za-z0-9._-]{1,100}$",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex RepositoryNamePattern();
}
