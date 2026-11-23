using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.BackgroundServices.LoggingExtensions;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.BackgroundServices;

public sealed class GitHubPollingWorker : PollingBackgroundService
{
    private const int DEFAULT_POLL_INTERVAL_SECONDS = 60;
    private const string PULL_REQUEST_TYPE = "PullRequest";
    private const string ISSUE_TYPE = "Issue";

    private readonly ILogger<GitHubPollingWorker> _logger;
    private readonly IModifiedIssueMentionPoller _modifiedIssueMentionPoller;
    private readonly INotificationFilter _notificationFilter;
    private readonly GitHubOptions _options;
    private readonly INotificationPoller _poller;
    private readonly IWorkItemRefresher _workItemRefresher;

    public GitHubPollingWorker(
        INotificationPoller poller,
        IModifiedIssueMentionPoller modifiedIssueMentionPoller,
        INotificationFilter notificationFilter,
        IWorkItemRefresher workItemRefresher,
        IOptions<GitHubOptions> options,
        ILogger<GitHubPollingWorker> logger
    )
        : base(
            configuredIntervalSeconds: options.Value.PollIntervalSeconds,
            defaultIntervalSeconds: DEFAULT_POLL_INTERVAL_SECONDS
        )
    {
        this._poller = poller;
        this._modifiedIssueMentionPoller = modifiedIssueMentionPoller;
        this._notificationFilter = notificationFilter;
        this._workItemRefresher = workItemRefresher;
        this._options = options.Value;
        this._logger = logger;
    }

    protected override async ValueTask<int?> DoWorkAsync(CancellationToken cancellationToken)
    {
        await this.PollAndProcessAsync(cancellationToken);

        return null;
    }

    protected override void LogStarting()
    {
        this._logger.LogWorkerStarting();
    }

    protected override void LogStopping()
    {
        this._logger.LogWorkerStopping();
    }

    protected override void LogError(Exception exception)
    {
        this._logger.LogPollingError(exception: exception);
    }

    private async ValueTask PollAndProcessAsync(CancellationToken cancellationToken)
    {
        NotificationPollResult pollResult = await this._poller.PollAsync(cancellationToken);
        this._logger.LogPolledNotifications(count: pollResult.Notifications.Count);
        await this.ProcessNotificationsAsync(
            notifications: pollResult.Notifications,
            cancellationToken: cancellationToken
        );

        if (pollResult.CandidateETag is not null)
        {
            await this._poller.CommitETagAsync(
                candidateETag: pollResult.CandidateETag,
                cancellationToken: cancellationToken
            );
        }

        if (this._options.Filter.PollIssueEdits)
        {
            IReadOnlyList<GitHubNotification> mentionNotifications = await this._modifiedIssueMentionPoller.PollAsync(
                cancellationToken
            );
            await this.ProcessNotificationsAsync(
                notifications: mentionNotifications,
                cancellationToken: cancellationToken
            );
        }
    }

    private async ValueTask ProcessNotificationsAsync(
        IReadOnlyList<GitHubNotification> notifications,
        CancellationToken cancellationToken
    )
    {
        foreach (GitHubNotification notification in notifications.Where(this._notificationFilter.ShouldProcess))
        {
            await this.TrackNotificationStateAsync(notification: notification, cancellationToken: cancellationToken);
        }
    }

    private async ValueTask TrackNotificationStateAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    )
    {
        if (
            string.Equals(
                a: notification.Subject.Type,
                b: PULL_REQUEST_TYPE,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            await this._workItemRefresher.RefreshPullRequestAsync(
                notification: notification,
                cancellationToken: cancellationToken
            );
        }
        else if (
            string.Equals(
                a: notification.Subject.Type,
                b: ISSUE_TYPE,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            await this._workItemRefresher.RefreshIssueAsync(
                notification: notification,
                cancellationToken: cancellationToken
            );
        }
    }
}
