using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly IDiscordDispatcher _discordDispatcher;
    private readonly ILogger<GitHubPollingWorker> _logger;
    private readonly INotificationFilter _notificationFilter;
    private readonly GitHubOptions _options;
    private readonly IGitHubNotificationPoller _poller;

    public GitHubPollingWorker(
        IGitHubNotificationPoller poller,
        INotificationFilter notificationFilter,
        IDiscordDispatcher discordDispatcher,
        IOptions<GitHubOptions> options,
        ILogger<GitHubPollingWorker> logger
    )
    {
        this._poller = poller;
        this._notificationFilter = notificationFilter;
        this._discordDispatcher = discordDispatcher;
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

            Discord.DataTypes.DiscordMessage message = BuildDiscordMessage(notification);

            await this._discordDispatcher.SendAsync(message: message, cancellationToken: cancellationToken);
        }
    }

    private static Discord.DataTypes.DiscordMessage BuildDiscordMessage(GitHubNotification notification)
    {
        Discord.DataTypes.DiscordEmbed embed = new(
            Title: notification.Subject.Title,
            Description: $"Reason: {notification.Reason}",
            Url: notification.Subject.Url,
            Color: 0x5865F2
        );

        return new Discord.DataTypes.DiscordMessage(Content: $"[{notification.Repository.FullName}] {notification.Subject.Type}", Embeds: [embed]);
    }
}
