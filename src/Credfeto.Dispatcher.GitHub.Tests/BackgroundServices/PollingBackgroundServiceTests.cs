using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.BackgroundServices;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.BackgroundServices;

public sealed class PollingBackgroundServiceTests : TestBase
{
    private const int LONG_INTERVAL_SECONDS = 60;

    [Fact]
    public async Task LogsErrorAndContinuesWhenWorkThrowsAsync()
    {
        TaskCompletionSource ranAgain = new();
        InvalidOperationException failure = new("Test work failure");

        CancellationToken token = TestContext.Current.CancellationToken;

        using StubPollingService service = new(
            configuredIntervalSeconds: 1,
            defaultIntervalSeconds: LONG_INTERVAL_SECONDS,
            work: (count, _) =>
            {
                if (count >= 2)
                {
                    ranAgain.TrySetResult();
                }

                return ValueTask.FromException<int?>(failure);
            }
        );
        await service.StartAsync(token);
        await ranAgain.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        Assert.Contains(expected: failure, collection: service.Errors);
        Assert.True(service.WorkCount >= 2, "Expected work to have run again after the failure");
    }

    [Fact]
    public async Task StopsCleanlyWhenCancelledDuringWorkAsync()
    {
        TaskCompletionSource workStarted = new();

        CancellationToken token = TestContext.Current.CancellationToken;

        using StubPollingService service = new(
            configuredIntervalSeconds: LONG_INTERVAL_SECONDS,
            defaultIntervalSeconds: LONG_INTERVAL_SECONDS,
            work: async (_, cancellationToken) =>
            {
                workStarted.TrySetResult();
                await Task.Delay(millisecondsDelay: Timeout.Infinite, cancellationToken: cancellationToken);

                return null;
            }
        );
        await service.StartAsync(token);
        await workStarted.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        Assert.True(service.ExecuteTask?.IsCompletedSuccessfully, "Expected the loop to exit cleanly on shutdown");
        Assert.Empty(service.Errors);
        Assert.Equal(expected: 1, actual: service.WorkCount);
        Assert.Equal(expected: 1, actual: service.StartingCount);
        Assert.Equal(expected: 1, actual: service.StoppingCount);
    }

    [Fact]
    public async Task StopsCleanlyWhenCancelledDuringDelayAsync()
    {
        TaskCompletionSource workCompleted = new();

        CancellationToken token = TestContext.Current.CancellationToken;

        using StubPollingService service = new(
            configuredIntervalSeconds: LONG_INTERVAL_SECONDS,
            defaultIntervalSeconds: LONG_INTERVAL_SECONDS,
            work: (_, _) =>
            {
                workCompleted.TrySetResult();

                return NoSuggestionAsync();
            }
        );
        await service.StartAsync(token);
        await workCompleted.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        Assert.True(service.ExecuteTask?.IsCompletedSuccessfully, "Expected the loop to exit cleanly on shutdown");
        Assert.Empty(service.Errors);
        Assert.Equal(expected: 1, actual: service.WorkCount);
        Assert.Equal(expected: 1, actual: service.StoppingCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public static async Task FallsBackToDefaultIntervalWhenConfiguredIntervalIsNotPositiveAsync(
        int configuredIntervalSeconds
    )
    {
        TaskCompletionSource ranAgain = new();

        CancellationToken token = TestContext.Current.CancellationToken;

        using StubPollingService service = new(
            configuredIntervalSeconds: configuredIntervalSeconds,
            defaultIntervalSeconds: 1,
            work: (count, _) =>
            {
                if (count >= 2)
                {
                    ranAgain.TrySetResult();
                }

                return NoSuggestionAsync();
            }
        );
        await service.StartAsync(token);
        await ranAgain.Task.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await service.StopAsync(token);

        Assert.True(service.WorkCount >= 2, "Expected work to repeat using the default interval");
        Assert.Empty(service.Errors);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(5, 1)]
    public static async Task WaitsForTheLongerOfConfiguredAndSuggestedIntervalAsync(
        int configuredIntervalSeconds,
        int suggestedIntervalSeconds
    )
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        using StubPollingService service = new(
            configuredIntervalSeconds: configuredIntervalSeconds,
            defaultIntervalSeconds: LONG_INTERVAL_SECONDS,
            work: (_, _) => ValueTask.FromResult<int?>(suggestedIntervalSeconds)
        );
        await service.StartAsync(token);
        await Task.Delay(millisecondsDelay: 1500, cancellationToken: token);
        await service.StopAsync(token);

        Assert.Equal(expected: 1, actual: service.WorkCount);
    }

    private static ValueTask<int?> NoSuggestionAsync()
    {
        return ValueTask.FromResult<int?>(null);
    }

    private sealed class StubPollingService : PollingBackgroundService
    {
        private readonly ConcurrentQueue<Exception> _errors = new();
        private readonly Func<int, CancellationToken, ValueTask<int?>> _work;
        private int _startingCount;
        private int _stoppingCount;
        private int _workCount;

        public StubPollingService(
            int configuredIntervalSeconds,
            int defaultIntervalSeconds,
            Func<int, CancellationToken, ValueTask<int?>> work
        )
            : base(configuredIntervalSeconds: configuredIntervalSeconds, defaultIntervalSeconds: defaultIntervalSeconds)
        {
            this._work = work;
        }

        public int WorkCount => Volatile.Read(ref this._workCount);

        public int StartingCount => Volatile.Read(ref this._startingCount);

        public int StoppingCount => Volatile.Read(ref this._stoppingCount);

        public IReadOnlyList<Exception> Errors => [.. this._errors];

        protected override ValueTask<int?> DoWorkAsync(CancellationToken cancellationToken)
        {
            return this._work(arg1: Interlocked.Increment(ref this._workCount), arg2: cancellationToken);
        }

        protected override void LogStarting()
        {
            Interlocked.Increment(ref this._startingCount);
        }

        protected override void LogStopping()
        {
            Interlocked.Increment(ref this._stoppingCount);
        }

        protected override void LogError(Exception exception)
        {
            this._errors.Enqueue(exception);
        }
    }
}
