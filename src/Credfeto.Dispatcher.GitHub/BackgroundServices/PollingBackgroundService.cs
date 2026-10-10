using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Credfeto.Dispatcher.GitHub.BackgroundServices;

public abstract class PollingBackgroundService : BackgroundService
{
    private readonly int _intervalSeconds;

    protected PollingBackgroundService(int configuredIntervalSeconds, int defaultIntervalSeconds)
    {
        this._intervalSeconds = configuredIntervalSeconds > 0 ? configuredIntervalSeconds : defaultIntervalSeconds;
    }

    // Nullable because only some pollers receive a server-requested back-off; it may extend but never shorten the configured interval.
    protected abstract ValueTask<int?> DoWorkAsync(CancellationToken cancellationToken);

    protected abstract void LogStarting();

    protected abstract void LogStopping();

    protected abstract void LogError(Exception exception);

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.LogStarting();

        while (!stoppingToken.IsCancellationRequested)
        {
            int? suggestedIntervalSeconds = null;

            try
            {
                suggestedIntervalSeconds = await this.DoWorkAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                this.LogError(exception);
            }

            int intervalSeconds = Math.Max(this._intervalSeconds, suggestedIntervalSeconds ?? this._intervalSeconds);

            try
            {
                await Task.Delay(millisecondsDelay: intervalSeconds * 1000, cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        this.LogStopping();
    }
}
