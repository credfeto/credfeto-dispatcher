using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.Configuration;
using Credfeto.Dispatcher.Storage.InMemory.LoggingExtensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.Storage.InMemory;

// Periodically serialises InMemoryDispatcherStore to disk (see DispatcherStoreSnapshotStore for
// the write mechanics). Skips a tick entirely when the store's version hasn't changed since the
// last successful write, so an idle store doesn't churn disk I/O every interval.
public sealed class SnapshotWriterService : BackgroundService
{
    private readonly InMemoryDispatcherStore _store;
    private readonly DispatcherStoreSnapshotStore _snapshotStore;
    private readonly int _intervalMilliseconds;
    private readonly ILogger<SnapshotWriterService> _logger;
    private int _lastWrittenVersion = -1;

    public SnapshotWriterService(
        InMemoryDispatcherStore store,
        DispatcherStoreSnapshotStore snapshotStore,
        IOptions<SnapshotOptions> options,
        ILogger<SnapshotWriterService> logger
    )
    {
        this._store = store;
        this._snapshotStore = snapshotStore;
        int intervalSeconds =
            options.Value.IntervalSeconds > 0 ? options.Value.IntervalSeconds : SnapshotOptions.DefaultIntervalSeconds;
        this._intervalMilliseconds = intervalSeconds * 1000;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogSnapshotWriterStarting();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.WriteIfChangedAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                this._logger.LogSnapshotWriteError(exception: exception);
            }

            try
            {
                await Task.Delay(millisecondsDelay: this._intervalMilliseconds, cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        this._logger.LogSnapshotWriterStopping();
    }

    // Internal (not private) so tests can exercise the dirty-check/save-skip logic for a single
    // tick directly, without driving BackgroundService's own ExecuteAsync timer loop.
    internal async ValueTask WriteIfChangedAsync(CancellationToken cancellationToken)
    {
        int currentVersion = this._store.Version;

        if (currentVersion == this._lastWrittenVersion)
        {
            return;
        }

        DispatcherStoreSnapshotData snapshot = this._store.ExportSnapshot();
        bool saved = await this._snapshotStore.SaveAsync(data: snapshot, cancellationToken: cancellationToken);

        // Only advance on an actual successful write - a persistent failure (unmounted volume,
        // read-only mount, full disk) must keep being retried on every subsequent tick rather
        // than being recorded as saved after the first attempt.
        if (saved)
        {
            this._lastWrittenVersion = currentVersion;
        }
    }
}
