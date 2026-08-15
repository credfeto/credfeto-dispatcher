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

public sealed class StartupNotificationServiceTests : TestBase
{
    private StartupNotificationService CreateService(IGitHubAuthVerifier verifier)
    {
        return new StartupNotificationService(
            authVerifier: verifier,
            logger: this.GetTypedLogger<StartupNotificationService>()
        );
    }

    private static async Task RunToCompletionAsync(
        StartupNotificationService service,
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

        using StartupNotificationService service = this.CreateService(verifier);
        await RunToCompletionAsync(service: service, verifier: verifier, cancellationToken: token);

        Assert.Equal(expected: 1, actual: verifier.CallCount);
    }

    [Theory]
    [InlineData(FailureMode.Unauthorized)]
    [InlineData(FailureMode.Forbidden)]
    [InlineData(FailureMode.Cancelled)]
    [InlineData(FailureMode.Unexpected)]
    public async Task DoesNotThrowWhenVerifierThrowsAsync(FailureMode failureMode)
    {
        FakeAuthVerifier verifier = new(exception: CreateException(failureMode));
        CancellationToken token = this.CancellationToken();

        using StartupNotificationService service = this.CreateService(verifier);
        await RunToCompletionAsync(service: service, verifier: verifier, cancellationToken: token);

        Assert.Equal(expected: 1, actual: verifier.CallCount);
    }

    private static Exception CreateException(FailureMode failureMode)
    {
        return failureMode switch
        {
            FailureMode.Unauthorized => new HttpRequestException(
                message: "unauthorized",
                inner: null,
                statusCode: HttpStatusCode.Unauthorized
            ),
            FailureMode.Forbidden => new HttpRequestException(
                message: "forbidden",
                inner: null,
                statusCode: HttpStatusCode.Forbidden
            ),
            FailureMode.Cancelled => new OperationCanceledException(),
            FailureMode.Unexpected => new InvalidOperationException("boom"),
            _ => throw new ArgumentOutOfRangeException(nameof(failureMode)),
        };
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
