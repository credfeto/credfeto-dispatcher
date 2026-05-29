using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.BackgroundServices.LoggingExtensions;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.BackgroundServices;

public sealed class GitHubPollingWorker : BackgroundService
{
    private const string PULL_REQUEST_TYPE = "PullRequest";
    private const string ISSUE_TYPE = "Issue";

    private readonly IIssueDetailFetcher _issueDetailFetcher;
    private readonly ILogger<GitHubPollingWorker> _logger;
    private readonly INotificationFilter _notificationFilter;
    private readonly INotificationStateTracker _notificationStateTracker;
    private readonly GitHubOptions _options;
    private readonly IModifiedIssueMentionPoller _modifiedIssueMentionPoller;
    private readonly INotificationPoller _poller;
    private readonly IPullRequestDetailFetcher _pullRequestDetailFetcher;

    public GitHubPollingWorker(
        INotificationPoller poller,
        IModifiedIssueMentionPoller modifiedIssueMentionPoller,
        INotificationFilter notificationFilter,
        IPullRequestDetailFetcher pullRequestDetailFetcher,
        IIssueDetailFetcher issueDetailFetcher,
        INotificationStateTracker notificationStateTracker,
        IOptions<GitHubOptions> options,
        ILogger<GitHubPollingWorker> logger
    )
    {
        this._poller = poller;
        this._modifiedIssueMentionPoller = modifiedIssueMentionPoller;
        this._notificationFilter = notificationFilter;
        this._pullRequestDetailFetcher = pullRequestDetailFetcher;
        this._issueDetailFetcher = issueDetailFetcher;
        this._notificationStateTracker = notificationStateTracker;
        this._options = options.Value;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogWorkerStarting();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.PollAndProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                this._logger.LogPollingError(exception: exception);
            }

            int pollIntervalSeconds = this._options.PollIntervalSeconds > 0 ? this._options.PollIntervalSeconds : 60;

            await Task.Delay(millisecondsDelay: pollIntervalSeconds * 1000, cancellationToken: stoppingToken);
        }

        this._logger.LogWorkerStopping();
    }

    private async ValueTask PollAndProcessAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<GitHubNotification> notifications = await this._poller.PollAsync(cancellationToken);
        this._logger.LogPolledNotifications(count: notifications.Count);
        await this.ProcessNotificationsAsync(notifications: notifications, cancellationToken: cancellationToken);

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
            await this.TrackPullRequestStateAsync(notification: notification, cancellationToken: cancellationToken);
        }
        else if (
            string.Equals(
                a: notification.Subject.Type,
                b: ISSUE_TYPE,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            await this.TrackIssueStateAsync(notification: notification, cancellationToken: cancellationToken);
        }
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical to TrackIssueStateAsync but operates on pull requests."
    )]
    private async ValueTask TrackPullRequestStateAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    )
    {
        PullRequestDetails? details = await this._pullRequestDetailFetcher.FetchAsync(
            notification: notification,
            cancellationToken: cancellationToken
        );

        if (details is not null)
        {
            WorkPriority priority = LabelParser.ParsePriority(details.Labels);
            bool isOnHold = LabelParser.IsOnHold(
                labels: details.Labels,
                noWorkFilter: this._options.Filter.NoWorkFilter
            );
            await this._notificationStateTracker.UpdateStateAsync(
                notification: notification,
                details: details,
                priority: priority,
                isOnHold: isOnHold,
                cancellationToken: cancellationToken
            );
        }
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical to TrackPullRequestStateAsync but operates on issues."
    )]
    private async ValueTask TrackIssueStateAsync(GitHubNotification notification, CancellationToken cancellationToken)
    {
        IssueDetails? details = await this._issueDetailFetcher.FetchAsync(
            notification: notification,
            cancellationToken: cancellationToken
        );

        if (details is not null)
        {
            WorkPriority priority = LabelParser.ParsePriority(details.Labels);
            bool isOnHold = LabelParser.IsOnHold(
                labels: details.Labels,
                noWorkFilter: this._options.Filter.NoWorkFilter
            );
            await this._notificationStateTracker.UpdateStateAsync(
                notification: notification,
                details: details,
                priority: priority,
                isOnHold: isOnHold,
                cancellationToken: cancellationToken
            );
        }
    }
}
