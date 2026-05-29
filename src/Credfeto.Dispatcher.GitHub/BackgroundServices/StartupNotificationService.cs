using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.BackgroundServices.LoggingExtensions;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.BackgroundServices;

public sealed class StartupNotificationService : BackgroundService
{
    private readonly ILogger<StartupNotificationService> _logger;
    private readonly INotificationPoller _poller;

    public StartupNotificationService(INotificationPoller poller, ILogger<StartupNotificationService> logger)
    {
        this._poller = poller;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await this.CheckGitHubAuthAsync(stoppingToken);
    }

    private async ValueTask CheckGitHubAuthAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this._poller.PollAsync(cancellationToken);
            this._logger.LogGitHubAuthenticationSuccessful();
        }
        catch (HttpRequestException httpException)
            when (httpException.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            int statusCode = (int)(httpException.StatusCode ?? HttpStatusCode.Unauthorized);
            this._logger.LogGitHubAuthenticationFailed(statusCode: statusCode);
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
}
