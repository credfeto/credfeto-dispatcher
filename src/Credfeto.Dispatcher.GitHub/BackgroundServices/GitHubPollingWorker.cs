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
    private readonly INotificationStateTracker _notificationStateTracker;
    private readonly GitHubOptions _options;
    private readonly INotificationPoller _poller;
    private readonly IPullRequestDetailFetcher _pullRequestDetailFetcher;

    public GitHubPollingWorker(
        INotificationPoller poller,
        INotificationFilter notificationFilter,
        IDiscordDispatcher discordDispatcher,
        IPullRequestDetailFetcher pullRequestDetailFetcher,
        IIssueDetailFetcher issueDetailFetcher,
        INotificationStateTracker notificationStateTracker,
        IOptions<GitHubOptions> options,
        ILogger<GitHubPollingWorker> logger
    )
    {
        this._poller = poller;
        this._notificationFilter = notificationFilter;
        this._discordDispatcher = discordDispatcher;
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
                await this.PollAndDispatchAsync(stoppingToken);
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

    private async ValueTask PollAndDispatchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<GitHubNotification> notifications = await this._poller.PollAsync(cancellationToken);

        this._logger.LogPolledNotifications(count: notifications.Count);

        foreach (GitHubNotification notification in notifications)
        {
            this._logger.LogDispatchingNotification(notificationId: notification.Id, repository: notification.Repository.FullName, title: notification.Subject.Title);

            await this.ProcessNotificationAsync(notification: notification, cancellationToken: cancellationToken);
        }
    }

    private async ValueTask ProcessNotificationAsync(GitHubNotification notification, CancellationToken cancellationToken)
    {
        if (string.Equals(a: notification.Subject.Type, b: PullRequestType, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            if (await this.TryProcessPullRequestNotificationAsync(notification: notification, cancellationToken: cancellationToken))
            {
                return;
            }
        }
        else if (string.Equals(a: notification.Subject.Type, b: IssueType, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            if (await this.TryProcessIssueNotificationAsync(notification: notification, cancellationToken: cancellationToken))
            {
                return;
            }
        }

        if (!this._notificationFilter.ShouldDispatch(notification))
        {
            return;
        }

        DiscordMessage fallbackMessage = BuildBasicMessage(notification);
        await this._discordDispatcher.SendAsync(message: fallbackMessage, cancellationToken: cancellationToken);
    }

    [SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "PR and Issue branches intentionally mirror each other to keep flow explicit.")]
    private async ValueTask<bool> TryProcessPullRequestNotificationAsync(GitHubNotification notification, CancellationToken cancellationToken)
    {
        PullRequestDetails? details = await this._pullRequestDetailFetcher.FetchAsync(notification: notification, cancellationToken: cancellationToken);

        if (details is null)
        {
            return false;
        }

        bool exists = await this._notificationStateTracker.PullRequestExistsAsync(notification: notification, number: details.Number, cancellationToken: cancellationToken);
        bool isExcluded = !this._notificationFilter.ShouldDispatch(notification) || details.OnHold;

        if (!exists && isExcluded)
        {
            return true;
        }

        if (!isExcluded)
        {
            if (!await this._notificationStateTracker.ShouldSkipPullRequestAsync(
                    notification: notification,
                    details: details,
                    cancellationToken: cancellationToken))
            {
                DiscordMessage message = BuildPullRequestMessage(notification: notification, details: details);
                await this._discordDispatcher.SendAsync(message: message, cancellationToken: cancellationToken);
            }
            else
            {
                this._logger.LogSkippingClosedItem(notificationId: notification.Id, repository: notification.Repository.FullName, itemId: details.Number, itemType: PullRequestType);
            }
        }

        await this._notificationStateTracker.UpdatePullRequestStateAsync(notification: notification, details: details, cancellationToken: cancellationToken);

        return true;
    }

    [SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "PR and Issue branches intentionally mirror each other to keep flow explicit.")]
    private async ValueTask<bool> TryProcessIssueNotificationAsync(GitHubNotification notification, CancellationToken cancellationToken)
    {
        IssueDetails? details = await this._issueDetailFetcher.FetchAsync(notification: notification, cancellationToken: cancellationToken);

        if (details is null)
        {
            return false;
        }

        bool exists = await this._notificationStateTracker.IssueExistsAsync(notification: notification, number: details.Number, cancellationToken: cancellationToken);
        bool isExcluded = !this._notificationFilter.ShouldDispatch(notification) || details.OnHold;

        if (!exists && isExcluded)
        {
            return true;
        }

        if (!isExcluded)
        {
            if (!await this._notificationStateTracker.ShouldSkipIssueAsync(
                    notification: notification,
                    details: details,
                    cancellationToken: cancellationToken))
            {
                DiscordMessage message = BuildBasicMessage(notification);
                await this._discordDispatcher.SendAsync(message: message, cancellationToken: cancellationToken);
            }
            else
            {
                this._logger.LogSkippingClosedItem(notificationId: notification.Id, repository: notification.Repository.FullName, itemId: details.Number, itemType: IssueType);
            }
        }

        await this._notificationStateTracker.UpdateIssueStateAsync(notification: notification, details: details, cancellationToken: cancellationToken);

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

        return new DiscordMessage(Content: $"[{notification.Repository.FullName}] {notification.Subject.Type}", Embeds: [embed]);
    }

    private static DiscordMessage BuildPullRequestMessage(GitHubNotification notification, PullRequestDetails details)
    {
        List<DiscordEmbedField> fields =
        [
            new DiscordEmbedField(Name: "Status", Value: details.Status.GetName(), Inline: true),
            new DiscordEmbedField(Name: "Reason", Value: FormatReason(notification.Reason), Inline: true),
        ];

        if (details.Assignees.Count > 0)
        {
            fields.Add(new DiscordEmbedField(Name: "Assigned to", Value: string.Join(separator: ", ", values: details.Assignees), Inline: true));
        }

        if (details.Labels.Count > 0)
        {
            fields.Add(new DiscordEmbedField(Name: "Labels", Value: string.Join(separator: ", ", values: details.Labels), Inline: true));
        }

        if (details.LinkedItems.Count > 0)
        {
            string linked = string.Join(separator: ", ", values: details.LinkedItems.Select(i => $"#{i.Number}"));
            fields.Add(new DiscordEmbedField(Name: "Linked Issues", Value: linked, Inline: true));
        }

        if (details.Comments.Count > 0)
        {
            PullRequestComment latest = details.Comments[^1];
            string commentValue = $"[View comment]({latest.Url})\n{latest.Body}";
            fields.Add(new DiscordEmbedField(Name: $"Comment by {latest.Author}", Value: commentValue, Inline: false));
        }

        foreach (PullRequestReview review in details.Reviews)
        {
            string header = $"Review ({review.State}) by {review.Author}";
            string value = review.Body is not null ? $"[View review]({review.Url})\n{review.Body}" : $"[View review]({review.Url})";
            fields.Add(new DiscordEmbedField(Name: header, Value: value, Inline: false));
        }

        IReadOnlyList<PullRequestRun> failedRuns = [..details.Runs.Where(r => string.Equals(a: r.Conclusion, b: "failure", comparisonType: StringComparison.OrdinalIgnoreCase))];

        foreach (PullRequestRun run in failedRuns)
        {
            string runLabel = run.IsRequired ? $"{run.Name} ⚠️ required" : run.Name;
            fields.Add(new DiscordEmbedField(Name: "Failed CI Run", Value: $"[{runLabel}]({run.Url})", Inline: false));
        }

        DiscordEmbed embed = new(
            Title: details.Title,
            Description: $"PR #{details.Number} in {notification.Repository.FullName}",
            Url: details.HtmlUrl,
            Color: EmbedColour,
            Fields: fields
        );

        return new DiscordMessage(Content: $"[{notification.Repository.FullName}] PR #{details.Number} ({notification.Reason}) {details.HtmlUrl}", Embeds: [embed]);
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
            _ => reason
        };
    }

}
