using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.BackgroundServices;
using Credfeto.Dispatcher.GitHub.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.BackgroundServices;

public sealed class StartupGitHubAuthCheckServiceTests : TestBase
{
    private StartupGitHubAuthCheckService CreateService(IGitHubAuthVerifier verifier)
    {
        return new StartupGitHubAuthCheckService(
            authVerifier: verifier,
            logger: this.GetTypedLogger<StartupGitHubAuthCheckService>()
        );
    }

    private static async Task RunToCompletionAsync(
        StartupGitHubAuthCheckService service,
        FakeAuthVerifier verifier,
        CancellationToken cancellationToken
    )
    {
        await service.StartAsync(cancellationToken);
        await verifier.Called.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);
        await service.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task CompletesWithoutErrorWhenVerifierSucceedsAsync()
    {
        FakeAuthVerifier verifier = new();
        CancellationToken token = this.CancellationToken();

        using StartupGitHubAuthCheckService service = this.CreateService(verifier);
        await RunToCompletionAsync(service: service, verifier: verifier, cancellationToken: token);

        Assert.Equal(expected: 1, actual: verifier.CallCount);
    }

    public static TheoryData<Exception> FailureExceptions() =>
        [
            new HttpRequestException(message: "unauthorized", inner: null, statusCode: HttpStatusCode.Unauthorized),
            new HttpRequestException(message: "forbidden", inner: null, statusCode: HttpStatusCode.Forbidden),
            new OperationCanceledException(),
            new InvalidOperationException("boom"),
        ];

    [Theory]
    [MemberData(nameof(FailureExceptions))]
    public async Task DoesNotThrowWhenVerifierThrowsAsync(Exception exception)
    {
        FakeAuthVerifier verifier = new(exception: exception);
        CancellationToken token = this.CancellationToken();

        using StartupGitHubAuthCheckService service = this.CreateService(verifier);
        await RunToCompletionAsync(service: service, verifier: verifier, cancellationToken: token);

        Assert.Equal(expected: 1, actual: verifier.CallCount);
    }

    private sealed class FakeAuthVerifier : IGitHubAuthVerifier
    {
        private readonly Exception? _exception;
        private int _callCount;

        public FakeAuthVerifier(Exception? exception = null)
        {
            this._exception = exception;
        }

        public TaskCompletionSource Called { get; } = new();

        public int CallCount => this._callCount;

        public ValueTask VerifyAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this._callCount);
            this.Called.TrySetResult();

            if (this._exception is not null)
            {
                return ValueTask.FromException(this._exception);
            }

            return ValueTask.CompletedTask;
        }
    }
}
