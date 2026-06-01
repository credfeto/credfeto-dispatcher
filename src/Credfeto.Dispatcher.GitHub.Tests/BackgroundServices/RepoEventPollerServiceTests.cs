using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.BackgroundServices;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.BackgroundServices;

public sealed class RepoEventPollerServiceTests : TestBase
{
    private RepoEventPollerService CreateService(IRepoEventPoller poller, GitHubOptions? options = null)
    {
        return new RepoEventPollerService(
            poller: poller,
            options: Options.Create(options ?? new GitHubOptions()),
            logger: this.GetTypedLogger<RepoEventPollerService>()
        );
    }

    [Fact]
    public async Task CallsPollerOnStartupAsync()
    {
        TaskCompletionSource pollStarted = new();
        FakePoller poller = new(onPoll: () => pollStarted.TrySetResult());

        CancellationToken token = TestContext.Current.CancellationToken;

        using RepoEventPollerService service = this.CreateService(poller);
        await service.StartAsync(token);
        await pollStarted.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        Assert.True(poller.PollCount >= 1, "Expected poller to have been called at least once");
    }

    [Fact]
    public async Task StopsGracefullyWhenPollerThrowsOperationCanceledExceptionAsync()
    {
        TaskCompletionSource pollCalled = new();
        FakePoller poller = new(onPoll: () => pollCalled.TrySetResult(), exception: new OperationCanceledException());

        CancellationToken token = TestContext.Current.CancellationToken;

        using RepoEventPollerService service = this.CreateService(poller);
        await service.StartAsync(token);
        await pollCalled.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        Assert.True(poller.PollCount >= 1, "Expected poller to have been called at least once");
    }

    [Fact]
    public async Task CallsPollerWhenPollIntervalSecondsIsZeroAsync()
    {
        TaskCompletionSource pollStarted = new();
        FakePoller poller = new(onPoll: () => pollStarted.TrySetResult());

        CancellationToken token = TestContext.Current.CancellationToken;

        using RepoEventPollerService service = this.CreateService(
            poller,
            new GitHubOptions { PollIntervalSeconds = 0 }
        );
        await service.StartAsync(token);
        await pollStarted.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        Assert.True(poller.PollCount >= 1, "Expected poller to have been called at least once");
    }

    [Fact]
    public async Task HandlesExceptionFromPollerWithoutCrashingAsync()
    {
        TaskCompletionSource exceptionThrown = new();
        FakePoller poller = new(
            onPoll: () => exceptionThrown.TrySetResult(),
            exception: new InvalidOperationException("Test poll failure")
        );

        CancellationToken token = TestContext.Current.CancellationToken;

        using RepoEventPollerService service = this.CreateService(poller);
        await service.StartAsync(token);
        await exceptionThrown.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        Assert.True(poller.PollCount >= 1, "Expected poller to have been called at least once");
    }

    private sealed class FakePoller : IRepoEventPoller
    {
        private readonly Exception? _exception;
        private readonly Action _onPoll;
        private int _pollCount;

        public FakePoller(Action onPoll, Exception? exception = null)
        {
            this._onPoll = onPoll;
            this._exception = exception;
        }

        public int PollCount => this._pollCount;

        public ValueTask PollAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this._pollCount);
            this._onPoll();

            if (this._exception is not null)
            {
                return ValueTask.FromException(this._exception);
            }

            return ValueTask.CompletedTask;
        }
    }
}
