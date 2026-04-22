using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Dispatcher.Discord.DataTypes;
using Credfeto.Dispatcher.Discord.Interfaces;
using Credfeto.Dispatcher.GitHub.BackgroundServices.LoggingExtensions;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.BackgroundServices;

public sealed class StartupNotificationService : BackgroundService
{
    private static readonly Uri GitHubNotificationsUri = new("https://github.com/notifications");
    private static readonly Uri GitHubTokenSettingsUri = new("https://github.com/settings/tokens");
    private static readonly Uri RepositoryUri = new("https://github.com/credfeto/credfeto-dispatcher");

    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly IDiscordDispatcher _discordDispatcher;
    private readonly ILogger<StartupNotificationService> _logger;
    private readonly IGitHubNotificationPoller _poller;

    public StartupNotificationService(
        IGitHubNotificationPoller poller,
        IDiscordDispatcher discordDispatcher,
        ICurrentTimeSource currentTimeSource,
        ILogger<StartupNotificationService> logger
    )
    {
        this._poller = poller;
        this._discordDispatcher = discordDispatcher;
        this._currentTimeSource = currentTimeSource;
        this._logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        this._logger.LogStartupNotificationServiceStarting();

        try
        {
            DiscordMessage message = BuildAppStartedMessage(this._currentTimeSource.UtcNow());

            await this._discordDispatcher.SendAsync(message: message, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            this._logger.LogStartupNotificationError(exception: exception);
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await this.CheckGitHubAuthAsync(stoppingToken);
    }

    private static DiscordMessage BuildAppStartedMessage(in DateTimeOffset timestamp)
    {
        string machineName = Environment.MachineName;
        Assembly assembly = typeof(StartupNotificationService).Assembly;
        string product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Credfeto Dispatcher";
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                         ?? assembly.GetName().Version?.ToString()
                         ?? "unknown";

        DiscordEmbed embed = new(
            Title: "Application started",
            Description: $"Version: {version}\nHost: {machineName}\nStarted at: {timestamp:yyyy-MM-dd HH:mm:ss} UTC",
            Url: RepositoryUri,
            Color: 0x57F287
        );

        return new DiscordMessage(Content: $"**{product}** is starting up", Embeds: [embed]);
    }

    private async ValueTask CheckGitHubAuthAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this._poller.PollAsync(cancellationToken);

            this._logger.LogGitHubAuthenticationSuccessful();

            await this.TrySendAuthSuccessMessageAsync(cancellationToken);
        }
        catch (HttpRequestException httpException) when (httpException.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            int statusCode = (int)(httpException.StatusCode ?? HttpStatusCode.Unauthorized);

            this._logger.LogGitHubAuthenticationFailed(statusCode: statusCode);

            await this.TrySendAuthFailureMessageAsync(statusCode: statusCode, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException exception)
        {
            this._logger.LogStartupNotificationError(exception: exception);
        }
        catch (Exception exception)
        {
            this._logger.LogGitHubAuthCheckError(exception: exception);
        }
    }

    private async ValueTask TrySendAuthSuccessMessageAsync(CancellationToken cancellationToken)
    {
        try
        {
            DiscordEmbed embed = new(
                Title: "GitHub authentication successful",
                Description: "The GitHub token is valid and the notifications endpoint responded successfully.",
                Url: GitHubNotificationsUri,
                Color: 0x57F287
            );

            DiscordMessage message = new(Content: "\u2705 GitHub auth check passed", Embeds: [embed]);

            await this._discordDispatcher.SendAsync(message: message, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            this._logger.LogStartupNotificationError(exception: exception);
        }
    }

    private async ValueTask TrySendAuthFailureMessageAsync(int statusCode, CancellationToken cancellationToken)
    {
        try
        {
            DiscordEmbed embed = new(
                Title: "GitHub authentication failed",
                Description: $"HTTP {statusCode} received from the GitHub notifications endpoint. Check that the token is valid and has the `notifications` scope.",
                Url: GitHubTokenSettingsUri,
                Color: 0xED4245
            );

            DiscordMessage message = new(Content: "\u274c GitHub auth check failed", Embeds: [embed]);

            await this._discordDispatcher.SendAsync(message: message, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            this._logger.LogStartupNotificationError(exception: exception);
        }
    }
}
