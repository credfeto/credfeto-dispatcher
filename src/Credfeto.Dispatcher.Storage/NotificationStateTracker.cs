using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Database;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database;

namespace Credfeto.Dispatcher.Storage;

public sealed class NotificationStateTracker : INotificationStateTracker
{
    private readonly IDatabase _database;

    public NotificationStateTracker(IDatabase database)
    {
        this._database = database;
    }

    public ValueTask UpdateStateAsync(
        GitHubNotification notification,
        PullRequestDetails details,
        WorkPriority priority,
        bool isOnHold,
        CancellationToken cancellationToken
    )
    {
        return this.UpdatePullRequestAndLinkedIssuesAsync(
            notification: notification,
            details: details,
            priority: priority,
            isOnHold: isOnHold,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask UpdatePullRequestAndLinkedIssuesAsync(
        GitHubNotification notification,
        PullRequestDetails details,
        WorkPriority priority,
        bool isOnHold,
        CancellationToken cancellationToken
    )
    {
        await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.PullRequests_UpsertAsync(
                    connection: c,
                    repository: notification.Repository.FullName,
                    id: details.Number,
                    status: details.Status,
                    priority: (int)priority,
                    isOnHold: isOnHold,
                    commentCount: details.Comments.Count,
                    reviewDecision: NotificationDetailMapping.ComputeReviewDecision(details.Reviews),
                    failedCheckCount: NotificationDetailMapping.CountFailedChecks(details.Runs),
                    failedCheckNames: NotificationDetailMapping.BuildFailedCheckNames(details.Runs),
                    failedCheckSha: NotificationDetailMapping.BuildFailedCheckSha(details.Runs),
                    author: details.Author,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );

        IReadOnlyList<int> linkedIssueNumbers = [.. details.LinkedItems.Select(static item => item.Number).Distinct()];

        foreach (int linkedIssueNumber in linkedIssueNumbers)
        {
            await this._database.ExecuteAsync(
                action: (c, ct) =>
                    DispatcherDatabase.Issues_LinkPullRequestAsync(
                        connection: c,
                        repository: notification.Repository.FullName,
                        id: linkedIssueNumber,
                        linkedPrNumber: details.Number,
                        cancellationToken: ct
                    ),
                cancellationToken: cancellationToken
            );
        }
    }

    public ValueTask UpdateStateAsync(
        GitHubNotification notification,
        IssueDetails details,
        WorkPriority priority,
        bool isOnHold,
        CancellationToken cancellationToken
    )
    {
        return this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.Issues_UpsertAsync(
                    connection: c,
                    repository: notification.Repository.FullName,
                    id: details.Number,
                    status: details.Status,
                    priority: (int)priority,
                    isOnHold: isOnHold,
                    linkedPrNumber: NotificationDetailMapping.ExtractPrNumber(details.LinkedPullRequestUrl),
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }
}
