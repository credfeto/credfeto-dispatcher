using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;

namespace Credfeto.Dispatcher.Storage.InMemory;

public sealed class InMemoryNotificationStateTracker : INotificationStateTracker
{
    private readonly InMemoryDispatcherStore _store;

    public InMemoryNotificationStateTracker(InMemoryDispatcherStore store)
    {
        this._store = store;
    }

    public ValueTask UpdateStateAsync(
        GitHubNotification notification,
        PullRequestDetails details,
        WorkPriority priority,
        bool isOnHold,
        bool? isAdopted,
        CancellationToken cancellationToken
    )
    {
        string repository = notification.Repository.FullName;

        this._store.UpsertPullRequest(
            repository: repository,
            id: details.Number,
            status: details.Status,
            priority: (int)priority,
            isOnHold: isOnHold,
            hasDetail: details.HasDetail,
            commentCount: details.Comments.Count,
            reviewDecision: NotificationDetailMapping.ComputeReviewDecision(details.Reviews),
            failedCheckCount: NotificationDetailMapping.CountFailedChecks(details.Runs),
            failedCheckNames: NotificationDetailMapping.BuildFailedCheckNames(details.Runs),
            failedCheckSha: NotificationDetailMapping.BuildFailedCheckSha(details.Runs),
            author: details.Author,
            isAdopted: isAdopted,
            assignees: details.Assignees
        );

        foreach (int linkedIssueNumber in details.LinkedItems.Select(static item => item.Number).Distinct())
        {
            this._store.LinkIssueToPullRequest(
                repository: repository,
                id: linkedIssueNumber,
                linkedPrNumber: details.Number
            );
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateStateAsync(
        GitHubNotification notification,
        IssueDetails details,
        WorkPriority priority,
        bool isOnHold,
        CancellationToken cancellationToken
    )
    {
        this._store.UpsertIssue(
            repository: notification.Repository.FullName,
            id: details.Number,
            status: details.Status,
            priority: (int)priority,
            isOnHold: isOnHold,
            linkedPrNumber: NotificationDetailMapping.ExtractPrNumber(details.LinkedPullRequestUrl),
            assignees: details.Assignees
        );

        return ValueTask.CompletedTask;
    }
}
