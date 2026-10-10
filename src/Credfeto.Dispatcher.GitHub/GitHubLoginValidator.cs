using System.Text.RegularExpressions;

namespace Credfeto.Dispatcher.GitHub;

public static partial class GitHubLoginValidator
{
    private const int MAX_LOGIN_LENGTH = 39;

    public static bool IsValid(string login)
    {
        return login.Length <= MAX_LOGIN_LENGTH && LoginPattern().IsMatch(login);
    }

    // GitHub logins: alphanumerics and single hyphens, never leading, trailing or doubled.
    [GeneratedRegex(
        pattern: "^[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}$",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex LoginPattern();
}
