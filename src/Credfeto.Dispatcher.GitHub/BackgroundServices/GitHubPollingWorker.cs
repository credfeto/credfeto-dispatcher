using System;
using System.Collections.Generic;
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
    private const int EmbedColour = 0x5865F2;

    private readonly IDiscordDispatcher _discordDispatcher;
    private readonly ILogger<GitHubPollingWorker> _logger;
    private readonly INotificationFilter _notificationFilter;
    private readonly GitHubOptions _options;
    private readonly INotificationPoller _poller;
    private readonly IPullRequestDetailFetcher _pullRequestDetailFetcher;

    public GitHubPollingWorker(
        INotificationPoller poller,
        INotificationFilter notificationFilter,
        IDiscordDispatcher discordDispatcher,
        IPullRequestDetailFetcher pullRequestDetailFetcher,
        IOptions<GitHubOptions> options,
        ILogger<GitHubPollingWorker> logger
    )
    {
        this._poller = poller;
        this._notificationFilter = notificationFilter;
        this._discordDispatcher = discordDispatcher;
        this._pullRequestDetailFetcher = pullRequestDetailFetcher;
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
            if (!this._notificationFilter.ShouldDispatch(notification))
            {
                continue;
            }

            this._logger.LogDispatchingNotification(notificationId: notification.Id, repository: notification.Repository.FullName, title: notification.Subject.Title);

            DiscordMessage message = await this.BuildDiscordMessageAsync(notification: notification, cancellationToken: cancellationToken);

            await this._discordDispatcher.SendAsync(message: message, cancellationToken: cancellationToken);
        }
    }

    private async ValueTask<DiscordMessage> BuildDiscordMessageAsync(GitHubNotification notification, CancellationToken cancellationToken)
    {
        if (string.Equals(a: notification.Subject.Type, b: PullRequestType, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            PullRequestDetails? details = await this._pullRequestDetailFetcher.FetchAsync(notification: notification, cancellationToken: cancellationToken);

            if (details is not null)
            {
                return BuildPullRequestMessage(notification: notification, details: details);
            }
        }

        return BuildBasicMessage(notification);
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
            new DiscordEmbedField(Name: "Status", Value: details.Status, Inline: true),
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

        if (details.CommentBody is not null)
        {
            fields.Add(new DiscordEmbedField(Name: $"Comment by {details.CommentAuthor}", Value: details.CommentUrl is not null ? $"[View comment]({details.CommentUrl})\n{details.CommentBody}" : details.CommentBody, Inline: false));
        }

        if (details.ReviewBody is not null)
        {
            string reviewHeader = details.ReviewState is not null ? $"Review ({details.ReviewState}) by {details.ReviewAuthor}" : $"Review by {details.ReviewAuthor}";
            fields.Add(new DiscordEmbedField(Name: reviewHeader, Value: details.ReviewUrl is not null ? $"[View review]({details.ReviewUrl})\n{details.ReviewBody}" : details.ReviewBody, Inline: false));
        }

        if (details.FailedRunName is not null && details.FailedRunUrl is not null)
        {
            fields.Add(new DiscordEmbedField(Name: "Failed CI Run", Value: $"[{details.FailedRunName}]({details.FailedRunUrl})", Inline: false));
        }

        DiscordEmbed embed = new(
            Title: details.Title,
            Description: $"PR #{details.Number} in {notification.Repository.FullName}",
            Url: details.HtmlUrl,
            Color: EmbedColour,
            Fields: fields
        );

        return new DiscordMessage(Content: $"[{notification.Repository.FullName}] Pull Request", Embeds: [embed]);
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
