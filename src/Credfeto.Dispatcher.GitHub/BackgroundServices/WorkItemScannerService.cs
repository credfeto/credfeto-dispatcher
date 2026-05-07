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

public sealed class WorkItemScannerService : BackgroundService
{
    private readonly ILogger<WorkItemScannerService> _logger;
    private readonly GitHubScanOptions _scanOptions;
    private readonly IWorkItemScanner _scanner;

    public WorkItemScannerService(
        IWorkItemScanner scanner,
        IOptions<GitHubOptions> options,
        ILogger<WorkItemScannerService> logger
    )
    {
        this._scanner = scanner;
        this._scanOptions = options.Value.Scan;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogScannerStarting();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this._scanner.ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                this._logger.LogScanError(exception: exception);
            }

            int intervalSeconds =
                this._scanOptions.ScanIntervalSeconds > 0
                    ? this._scanOptions.ScanIntervalSeconds
                    : 3600;

            await Task.Delay(
                millisecondsDelay: intervalSeconds * 1000,
                cancellationToken: stoppingToken
            );
        }

        this._logger.LogScannerStopping();
    }
}
