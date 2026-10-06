using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.BackgroundServices;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Tests.Helpers;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.BackgroundServices;

public sealed class WorkItemScannerServiceTests : TestBase
{
    private readonly ILogger<WorkItemScannerService> _logger;
    private readonly IWorkItemScanner _scanner;

    public WorkItemScannerServiceTests()
    {
        this._scanner = GetSubstitute<IWorkItemScanner>();
        this._logger = GetSubstitute<ILogger<WorkItemScannerService>>();
        this._logger.MockLoggerIsEnabled();
    }

    private WorkItemScannerService CreateService(GitHubOptions? options = null)
    {
        return new WorkItemScannerService(
            scanner: this._scanner,
            options: Options.Create(options ?? new GitHubOptions()),
            logger: this._logger
        );
    }

    [Fact]
    public async Task CallsScannerOnStartupAsync()
    {
        TaskCompletionSource scanStarted = new();

        this._scanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                scanStarted.TrySetResult();

                return Task.CompletedTask;
            });

        CancellationToken token = TestContext.Current.CancellationToken;

        using WorkItemScannerService service = this.CreateService();
        await service.StartAsync(token);
        await scanStarted.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        await this._scanner.Received(1).ScanAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContinuesScanningWhenScannerThrowsOperationCanceledExceptionWithoutShutdownAsync()
    {
        TaskCompletionSource scannedAgain = new();
        int scanCount = 0;

        this._scanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                if (Interlocked.Increment(ref scanCount) >= 2)
                {
                    scannedAgain.TrySetResult();
                }

                return Task.FromException(new OperationCanceledException());
            });

        CancellationToken token = TestContext.Current.CancellationToken;

        using WorkItemScannerService service = this.CreateService(
            new GitHubOptions { Scan = new GitHubScanOptions { ScanIntervalSeconds = 1 } }
        );
        await service.StartAsync(token);
        await scannedAgain.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        Assert.Contains(this._logger.LoggedErrors(), exception => exception is OperationCanceledException);
    }

    [Fact]
    public async Task StopsWhenStoppingTokenIsCancelledDuringScanAsync()
    {
        TaskCompletionSource scanStarted = new();

        this._scanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                scanStarted.TrySetResult();

                return Task.Delay(
                    millisecondsDelay: Timeout.Infinite,
                    cancellationToken: callInfo.Arg<CancellationToken>()
                );
            });

        CancellationToken token = TestContext.Current.CancellationToken;

        using WorkItemScannerService service = this.CreateService();
        await service.StartAsync(token);
        await scanStarted.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        Assert.True(service.ExecuteTask?.IsCompletedSuccessfully, "Expected the scan loop to exit cleanly on shutdown");
        Assert.Empty(this._logger.LoggedErrors());
        await this._scanner.Received(1).ScanAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CallsScannerWhenScanIntervalSecondsIsZeroAsync()
    {
        TaskCompletionSource scanStarted = new();

        this._scanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                scanStarted.TrySetResult();

                return Task.CompletedTask;
            });

        CancellationToken token = TestContext.Current.CancellationToken;

        using WorkItemScannerService service = this.CreateService(
            new GitHubOptions { Scan = new GitHubScanOptions { ScanIntervalSeconds = 0 } }
        );
        await service.StartAsync(token);
        await scanStarted.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        await this._scanner.Received(1).ScanAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlesExceptionFromScannerWithoutCrashingAsync()
    {
        TaskCompletionSource exceptionThrown = new();

        this._scanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                exceptionThrown.TrySetResult();

                return Task.FromException(new InvalidOperationException("Test scan failure"));
            });

        CancellationToken token = TestContext.Current.CancellationToken;

        using WorkItemScannerService service = this.CreateService();
        await service.StartAsync(token);
        await exceptionThrown.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        await this._scanner.Received(1).ScanAsync(Arg.Any<CancellationToken>());
    }
}
