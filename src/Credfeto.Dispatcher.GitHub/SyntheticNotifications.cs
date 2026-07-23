using System;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub;

// Builds a placeholder notification pointing at a pull request's API URL, so IPullRequestDetailFetcher
// (built around GitHubNotification) can be reused for enrichment lookups outside the notification poller.
internal static class SyntheticNotifications
{
    public static GitHubNotification BuildPullRequestApiNotification(string idPrefix, string repo, int number)
    {
        Uri apiUrl = new($"https://api.github.com/repos/{repo}/pulls/{number}");
        Uri repoUri = new($"https://github.com/{repo}");

        return new GitHubNotification(
            Id: $"{idPrefix}:{repo}:pr:{number}",
            Reason: "scan",
            Subject: new NotificationSubject(Title: string.Empty, Url: apiUrl, Type: "PullRequest"),
            Repository: new NotificationRepository(FullName: repo, Url: repoUri),
            UpdatedAt: DateTimeOffset.MinValue,
            Unread: false
        );
    }
}
