using System;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub;

// Builds a placeholder notification pointing at a pull request's or issue's API URL, so the detail fetchers
// (built around GitHubNotification) can be reused for lookups outside the notification poller.
internal static class SyntheticNotifications
{
    public static GitHubNotification BuildPullRequestApiNotification(string idPrefix, string repo, int number)
    {
        return Build(
            id: $"{idPrefix}:{repo}:pr:{number}",
            reason: "scan",
            apiUrl: new($"https://api.github.com/repos/{repo}/pulls/{number}"),
            type: "PullRequest",
            repo: repo,
            updatedAt: DateTimeOffset.MinValue
        );
    }

    public static GitHubNotification BuildIssueApiNotification(
        string idPrefix,
        string repo,
        int number,
        in DateTimeOffset updatedAt
    )
    {
        return Build(
            id: $"{idPrefix}:{repo}:issue:{number}",
            reason: "event",
            apiUrl: new($"https://api.github.com/repos/{repo}/issues/{number}"),
            type: "Issue",
            repo: repo,
            updatedAt: updatedAt
        );
    }

    private static GitHubNotification Build(
        string id,
        string reason,
        Uri apiUrl,
        string type,
        string repo,
        in DateTimeOffset updatedAt
    )
    {
        Uri repoUri = new($"https://github.com/{repo}");

        return new GitHubNotification(
            Id: id,
            Reason: reason,
            Subject: new NotificationSubject(Title: string.Empty, Url: apiUrl, Type: type),
            Repository: new NotificationRepository(FullName: repo, Url: repoUri),
            UpdatedAt: updatedAt,
            Unread: false
        );
    }
}
