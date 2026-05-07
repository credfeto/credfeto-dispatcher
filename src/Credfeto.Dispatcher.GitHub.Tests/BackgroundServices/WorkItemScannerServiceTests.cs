using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.BackgroundServices;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.BackgroundServices;

public sealed class WorkItemScannerServiceTests : TestBase
{
    private readonly IWorkItemScanner _scanner;

    public WorkItemScannerServiceTests()
    {
        this._scanner = GetSubstitute<IWorkItemScanner>();
    }

    private WorkItemScannerService CreateService(GitHubOptions? options = null)
    {
        return new WorkItemScannerService(
            scanner: this._scanner,
            options: Options.Create(options ?? new GitHubOptions()),
            logger: this.GetTypedLogger<WorkItemScannerService>()
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
        await scanStarted.Task.WaitAsync(
            timeout: TimeSpan.FromSeconds(5),
            cancellationToken: token
        );
        await service.StopAsync(token);

        await this._scanner.Received(1).ScanAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopsGracefullyWhenScannerThrowsOperationCanceledExceptionAsync()
    {
        TaskCompletionSource scanCalled = new();

        this._scanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                scanCalled.TrySetResult();

                return Task.FromException(new OperationCanceledException());
            });

        CancellationToken token = TestContext.Current.CancellationToken;

        using WorkItemScannerService service = this.CreateService();
        await service.StartAsync(token);
        await scanCalled.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
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
        await exceptionThrown.Task.WaitAsync(
            timeout: TimeSpan.FromSeconds(5),
            cancellationToken: token
        );
        await service.StopAsync(token);

        await this._scanner.Received(1).ScanAsync(Arg.Any<CancellationToken>());
    }
}
