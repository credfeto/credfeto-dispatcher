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
    private readonly IGitHubAuthVerifier _authVerifier;
    private readonly ILogger<StartupNotificationService> _logger;

    public StartupNotificationService(IGitHubAuthVerifier authVerifier, ILogger<StartupNotificationService> logger)
    {
        this._authVerifier = authVerifier;
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
            await this._authVerifier.VerifyAsync(cancellationToken);
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
