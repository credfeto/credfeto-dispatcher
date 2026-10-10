using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.BackgroundServices.LoggingExtensions;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.BackgroundServices;

public sealed class RepoEventPollerService : PollingBackgroundService
{
    private const int DEFAULT_POLL_INTERVAL_SECONDS = 60;

    private readonly ILogger<RepoEventPollerService> _logger;
    private readonly IRepoEventPoller _poller;

    public RepoEventPollerService(
        IRepoEventPoller poller,
        IOptions<GitHubOptions> options,
        ILogger<RepoEventPollerService> logger
    )
        : base(
            configuredIntervalSeconds: options.Value.PollIntervalSeconds,
            defaultIntervalSeconds: DEFAULT_POLL_INTERVAL_SECONDS
        )
    {
        this._poller = poller;
        this._logger = logger;
    }

    protected override ValueTask<int?> DoWorkAsync(CancellationToken cancellationToken)
    {
        return this._poller.PollAsync(cancellationToken);
    }

    protected override void LogStarting()
    {
        this._logger.LogEventPollerStarting();
    }

    protected override void LogStopping()
    {
        this._logger.LogEventPollerStopping();
    }

    protected override void LogError(Exception exception)
    {
        this._logger.LogEventPollerError(exception: exception);
    }
}
