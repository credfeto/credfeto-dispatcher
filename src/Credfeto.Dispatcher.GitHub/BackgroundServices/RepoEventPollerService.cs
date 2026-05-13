using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.BackgroundServices.LoggingExtensions;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.BackgroundServices;

public sealed class RepoEventPollerService : BackgroundService
{
    private readonly ILogger<RepoEventPollerService> _logger;
    private readonly GitHubOptions _options;
    private readonly IRepoEventPoller _poller;

    public RepoEventPollerService(
        IRepoEventPoller poller,
        IOptions<GitHubOptions> options,
        ILogger<RepoEventPollerService> logger
    )
    {
        this._poller = poller;
        this._options = options.Value;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogEventPollerStarting();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this._poller.PollAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                this._logger.LogEventPollerError(exception: exception);
            }

            int pollIntervalSeconds = this._options.PollIntervalSeconds > 0 ? this._options.PollIntervalSeconds : 60;

            await Task.Delay(millisecondsDelay: pollIntervalSeconds * 1000, cancellationToken: stoppingToken);
        }

        this._logger.LogEventPollerStopping();
    }
}
