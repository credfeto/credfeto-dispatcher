using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Models;

namespace Credfeto.Dispatcher.GitHub.Services;

internal static class PullRequestDetailsFactory
{
    public static PullRequestDetails Build(
        ApiPullRequest pr,
        string repo,
        IReadOnlyList<string> labelNames,
        string status,
        Uri htmlUrl,
        IReadOnlyList<LinkedItem> linkedItems,
        string lastNotificationId,
        in DateTimeOffset lastNotificationTimestamp
    )
    {
        int slash = repo.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);
        string owner = slash >= 0 ? repo[..slash] : repo;
        string name = slash >= 0 ? repo[(slash + 1)..] : repo;
        Uri repoUri = new($"https://github.com/{repo}");

        return new PullRequestDetails(
            Number: pr.Number,
            Title: pr.Title,
            Status: status,
            HtmlUrl: htmlUrl,
            Assignees: [.. pr.Assignees.Select(a => a.Login)],
            Labels: labelNames,
            Body: null,
            Comments: [],
            Reviews: [],
            Runs: [],
            LinkedItems: linkedItems,
            Repository: new ItemRepository(Owner: owner, Name: name, Url: repoUri),
            LastNotification: new LastNotification(Id: lastNotificationId, Timestamp: lastNotificationTimestamp),
            Author: pr.User?.Login,
            CommitAuthors: []
        );
    }
}
