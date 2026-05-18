using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Discord.DataTypes;
using Credfeto.Dispatcher.Discord.Interfaces;
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
    private const string PullRequestType = "PullRequest";
    private const string IssueType = "Issue";
    private const int EmbedColour = 0x5865F2;

    private readonly IDiscordDispatcher _discordDispatcher;
    private readonly IIssueDetailFetcher _issueDetailFetcher;
    private readonly ILogger<GitHubPollingWorker> _logger;
    private readonly INotificationFilter _notificationFilter;
    private readonly IPendingNotificationStore _pendingNotificationStore;
    private readonly INotificationStateTracker _notificationStateTracker;
    private readonly NotificationQueueOptions _notificationQueueOptions;
    private readonly GitHubOptions _options;
    private readonly IModifiedIssueMentionPoller _modifiedIssueMentionPoller;
    private readonly INotificationPoller _poller;
    private readonly IPullRequestDetailFetcher _pullRequestDetailFetcher;
    private readonly TimeProvider _timeProvider;

    public GitHubPollingWorker(
        INotificationPoller poller,
        IModifiedIssueMentionPoller modifiedIssueMentionPoller,
        INotificationFilter notificationFilter,
        IDiscordDispatcher discordDispatcher,
        IPullRequestDetailFetcher pullRequestDetailFetcher,
        IIssueDetailFetcher issueDetailFetcher,
        INotificationStateTracker notificationStateTracker,
        IPendingNotificationStore pendingNotificationStore,
        TimeProvider timeProvider,
        IOptions<GitHubOptions> options,
        IOptions<NotificationQueueOptions> notificationQueueOptions,
        ILogger<GitHubPollingWorker> logger
    )
    {
        this._poller = poller;
        this._modifiedIssueMentionPoller = modifiedIssueMentionPoller;
        this._notificationFilter = notificationFilter;
        this._discordDispatcher = discordDispatcher;
        this._pullRequestDetailFetcher = pullRequestDetailFetcher;
        this._issueDetailFetcher = issueDetailFetcher;
        this._notificationStateTracker = notificationStateTracker;
        this._pendingNotificationStore = pendingNotificationStore;
        this._timeProvider = timeProvider;
        this._options = options.Value;
        this._notificationQueueOptions = notificationQueueOptions.Value;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogWorkerStarting();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.PollAndEnqueueAsync(stoppingToken);
                await this.DispatchReadyItemsAsync(stoppingToken);
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

    private async ValueTask PollAndEnqueueAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<GitHubNotification> notifications = await this._poller.PollAsync(cancellationToken);

        this._logger.LogPolledNotifications(count: notifications.Count);

        int delaySeconds =
            this._notificationQueueOptions.DelaySeconds > 0 ? this._notificationQueueOptions.DelaySeconds : 300;
        DateTimeOffset dispatchAfter = this._timeProvider.GetUtcNow().AddSeconds(delaySeconds);

        await this.EnqueueNotificationsAsync(
            notifications: notifications,
            dispatchAfter: dispatchAfter,
            cancellationToken: cancellationToken
        );

        if (this._options.Filter.PollIssueEdits)
        {
            IReadOnlyList<GitHubNotification> mentionNotifications = await this._modifiedIssueMentionPoller.PollAsync(
                cancellationToken
            );

            await this.EnqueueNotificationsAsync(
                notifications: mentionNotifications,
                dispatchAfter: dispatchAfter,
                cancellationToken: cancellationToken
            );
        }
    }

    private async ValueTask EnqueueNotificationsAsync(
        IReadOnlyList<GitHubNotification> notifications,
        DateTimeOffset dispatchAfter,
        CancellationToken cancellationToken
    )
    {
        foreach (GitHubNotification notification in notifications)
        {
            if (this._notificationFilter.ShouldDispatch(notification))
            {
                this._logger.LogEnqueueingNotification(
                    notificationId: notification.Id,
                    repository: notification.Repository.FullName,
                    title: notification.Subject.Title
                );
                await this._pendingNotificationStore.EnqueueAsync(
                    notification: notification,
                    dispatchAfter: dispatchAfter,
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await this._pendingNotificationStore.RemoveIfPresentAsync(
                    notification: notification,
                    cancellationToken: cancellationToken
                );
            }
        }
    }

    private async ValueTask DispatchReadyItemsAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        IReadOnlyList<GitHubNotification> readyItems = await this._pendingNotificationStore.GetReadyItemsAsync(
            now: now,
            cancellationToken: cancellationToken
        );

        foreach (GitHubNotification notification in readyItems)
        {
            this._logger.LogDispatchingNotification(
                notificationId: notification.Id,
                repository: notification.Repository.FullName,
                title: notification.Subject.Title
            );
            await this.ProcessNotificationAsync(notification: notification, cancellationToken: cancellationToken);
            await this._pendingNotificationStore.RemoveAsync(
                notification: notification,
                cancellationToken: cancellationToken
            );
        }
    }

    private async ValueTask ProcessNotificationAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    )
    {
        if (
            string.Equals(
                a: notification.Subject.Type,
                b: PullRequestType,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            if (
                await this.TryProcessPullRequestNotificationAsync(
                    notification: notification,
                    cancellationToken: cancellationToken
                )
            )
            {
                return;
            }
        }
        else if (
            string.Equals(
                a: notification.Subject.Type,
                b: IssueType,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            if (
                await this.TryProcessIssueNotificationAsync(
                    notification: notification,
                    cancellationToken: cancellationToken
                )
            )
            {
                return;
            }
        }

        DiscordMessage fallbackMessage = BuildBasicMessage(notification);
        await this._discordDispatcher.SendAsync(message: fallbackMessage, cancellationToken: cancellationToken);
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical to TryProcessIssueNotificationAsync but operates on pull requests."
    )]
    private async ValueTask<bool> TryProcessPullRequestNotificationAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    )
    {
        PullRequestDetails? details = await this._pullRequestDetailFetcher.FetchAsync(
            notification: notification,
            cancellationToken: cancellationToken
        );

        if (details is null)
        {
            return false;
        }

        if (
            !await this._notificationStateTracker.ShouldSkipAsync(
                notification: notification,
                details: details,
                cancellationToken: cancellationToken
            )
        )
        {
            DiscordMessage message = BuildPullRequestMessage(notification: notification, details: details);
            await this._discordDispatcher.SendAsync(message: message, cancellationToken: cancellationToken);
        }
        else
        {
            this._logger.LogSkippingClosedItem(
                notificationId: notification.Id,
                repository: notification.Repository.FullName,
                itemId: details.Number,
                itemType: PullRequestType
            );
        }

        WorkPriority prPriority = LabelParser.ParsePriority(details.Labels);
        bool prIsOnHold = LabelParser.IsOnHold(labels: details.Labels, noWorkFilter: this._options.Filter.NoWorkFilter);

        await this._notificationStateTracker.UpdateStateAsync(
            notification: notification,
            details: details,
            priority: prPriority,
            isOnHold: prIsOnHold,
            cancellationToken: cancellationToken
        );

        return true;
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical to TryProcessPullRequestNotificationAsync but operates on issues."
    )]
    private async ValueTask<bool> TryProcessIssueNotificationAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    )
    {
        IssueDetails? details = await this._issueDetailFetcher.FetchAsync(
            notification: notification,
            cancellationToken: cancellationToken
        );

        if (details is null)
        {
            return false;
        }

        if (
            !await this._notificationStateTracker.ShouldSkipAsync(
                notification: notification,
                details: details,
                cancellationToken: cancellationToken
            )
        )
        {
            DiscordMessage message = BuildIssueMessage(notification: notification, details: details);
            await this._discordDispatcher.SendAsync(message: message, cancellationToken: cancellationToken);
        }
        else
        {
            this._logger.LogSkippingClosedItem(
                notificationId: notification.Id,
                repository: notification.Repository.FullName,
                itemId: details.Number,
                itemType: IssueType
            );
        }

        WorkPriority issuePriority = LabelParser.ParsePriority(details.Labels);
        bool issueIsOnHold = LabelParser.IsOnHold(
            labels: details.Labels,
            noWorkFilter: this._options.Filter.NoWorkFilter
        );
        bool issueHasAssignee = details.Assignees.Count > 0;
        bool issueIsAiWork = LabelParser.IsAiWork(details.Labels);

        await this._notificationStateTracker.UpdateStateAsync(
            notification: notification,
            details: details,
            priority: issuePriority,
            isOnHold: issueIsOnHold,
            hasAssignee: issueHasAssignee,
            isAiWork: issueIsAiWork,
            cancellationToken: cancellationToken
        );

        return true;
    }

    private static DiscordMessage BuildBasicMessage(GitHubNotification notification)
    {
        DiscordEmbed embed = new(
            Title: notification.Subject.Title,
            Description: $"Reason: {notification.Reason}",
            Url: notification.Subject.Url,
            Color: EmbedColour
        );

        return new DiscordMessage(
            Content: $"[{notification.Repository.FullName}] {notification.Subject.Type}",
            Embeds: [embed]
        );
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical to BuildPullRequestMessage but builds messages for issues."
    )]
    private static DiscordMessage BuildIssueMessage(GitHubNotification notification, IssueDetails details)
    {
        List<DiscordEmbedField> fields = [];
        AddEmbed(fields: fields, name: "Status", value: details.Status, inline: true);
        AddEmbed(fields: fields, name: "Reason", value: FormatReason(notification.Reason), inline: true);

        if (details.Assignees.Count > 0)
        {
            AddEmbed(
                fields: fields,
                name: "Assigned to",
                value: string.Join(separator: ", ", values: details.Assignees),
                inline: true
            );
        }

        if (details.Labels.Count > 0)
        {
            AddEmbed(
                fields: fields,
                name: "Labels",
                value: string.Join(separator: ", ", values: details.Labels),
                inline: true
            );
        }

        if (details.LinkedPullRequestUrl is not null)
        {
            AddEmbed(
                fields: fields,
                name: "Linked PR",
                value: $"[View PR]({details.LinkedPullRequestUrl})",
                inline: false
            );
        }

        DiscordEmbed embed = new(
            Title: details.Title,
            Description: $"Issue #{details.Number} in {notification.Repository.FullName}",
            Url: details.HtmlUrl,
            Color: EmbedColour,
            Fields: fields
        );

        return new DiscordMessage(
            Content: $"[{notification.Repository.FullName}] Issue #{details.Number} ({notification.Reason}) {details.HtmlUrl}",
            Embeds: [embed]
        );
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical to BuildIssueMessage but builds messages for pull requests."
    )]
    private static DiscordMessage BuildPullRequestMessage(GitHubNotification notification, PullRequestDetails details)
    {
        List<DiscordEmbedField> fields = BuildPullRequestFields(notification: notification, details: details);

        DiscordEmbed embed = new(
            Title: details.Title,
            Description: $"PR #{details.Number} in {notification.Repository.FullName}",
            Url: details.HtmlUrl,
            Color: EmbedColour,
            Fields: fields
        );

        return new DiscordMessage(
            Content: $"[{notification.Repository.FullName}] PR #{details.Number} ({notification.Reason}) {details.HtmlUrl}",
            Embeds: [embed]
        );
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Shared field-building pattern with BuildIssueMessage."
    )]
    private static List<DiscordEmbedField> BuildPullRequestFields(
        GitHubNotification notification,
        PullRequestDetails details
    )
    {
        List<DiscordEmbedField> fields = [];
        AddEmbed(fields: fields, name: "Status", value: details.Status, inline: true);
        AddEmbed(fields: fields, name: "Reason", value: FormatReason(notification.Reason), inline: true);

        if (details.Assignees.Count > 0)
        {
            AddEmbed(
                fields: fields,
                name: "Assigned to",
                value: string.Join(separator: ", ", values: details.Assignees),
                inline: true
            );
        }

        if (details.Labels.Count > 0)
        {
            AddEmbed(
                fields: fields,
                name: "Labels",
                value: string.Join(separator: ", ", values: details.Labels),
                inline: true
            );
        }

        AddPullRequestSpecificFields(fields: fields, details: details);

        return fields;
    }

    private static void AddPullRequestSpecificFields(List<DiscordEmbedField> fields, PullRequestDetails details)
    {
        foreach (PullRequestComment comment in details.Comments)
        {
            string commentValue = $"[View comment]({comment.Url})\n{comment.Body}";
            AddEmbed(fields: fields, name: $"Comment by {comment.Author}", value: commentValue, inline: false);
        }

        foreach (PullRequestReview review in details.Reviews)
        {
            string reviewHeader = $"Review ({review.State}) by {review.Author}";
            string reviewValue = review.Body is not null
                ? $"[View review]({review.Url})\n{review.Body}"
                : $"[View review]({review.Url})";
            AddEmbed(fields: fields, name: reviewHeader, value: reviewValue, inline: false);
        }

        foreach (
            PullRequestRun run in details.Runs.Where(r =>
                string.Equals(a: r.Conclusion, b: "failure", comparisonType: StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            string requiredMarker = run.IsRequired ? " (required)" : string.Empty;
            AddEmbed(
                fields: fields,
                name: $"Failed CI Run{requiredMarker}",
                value: $"[{run.Name}]({run.Url})",
                inline: false
            );
        }
    }

    private static void AddEmbed(List<DiscordEmbedField> fields, string name, string value, bool inline)
    {
        fields.Add(new DiscordEmbedField(Name: name, Value: value, Inline: inline));
    }

    private static string FormatReason(string reason)
    {
        return reason switch
        {
            "assign" => "Assigned to you",
            "author" => "You are the author",
            "comment" => "New comment",
            "mention" => "You were mentioned",
            "review_requested" => "Review requested",
            "changes_requested" => "Changes requested",
            "ci_activity" => "CI activity",
            "subscribed" => "Subscribed",
            _ => reason,
        };
    }
}
