using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.BackgroundServices.LoggingExtensions;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.BackgroundServices;

public sealed class WorkItemScannerService : PollingBackgroundService
{
    private const int DEFAULT_SCAN_INTERVAL_SECONDS = 1800;

    private readonly ILogger<WorkItemScannerService> _logger;
    private readonly IWorkItemScanner _scanner;

    public WorkItemScannerService(
        IWorkItemScanner scanner,
        IOptions<GitHubOptions> options,
        ILogger<WorkItemScannerService> logger
    )
        : base(
            configuredIntervalSeconds: options.Value.Scan.ScanIntervalSeconds,
            defaultIntervalSeconds: DEFAULT_SCAN_INTERVAL_SECONDS
        )
    {
        this._scanner = scanner;
        this._logger = logger;
    }

    protected override async ValueTask<int?> DoWorkAsync(CancellationToken cancellationToken)
    {
        await this._scanner.ScanAsync(cancellationToken);

        return null;
    }

    protected override void LogStarting()
    {
        this._logger.LogScannerStarting();
    }

    protected override void LogStopping()
    {
        this._logger.LogScannerStopping();
    }

    protected override void LogError(Exception exception)
    {
        this._logger.LogScanError(exception: exception);
    }
}
