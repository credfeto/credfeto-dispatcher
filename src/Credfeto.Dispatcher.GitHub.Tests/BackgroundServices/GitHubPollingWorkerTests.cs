using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Discord.DataTypes;
using Credfeto.Dispatcher.Discord.Interfaces;
using Credfeto.Dispatcher.GitHub.BackgroundServices;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.BackgroundServices;

public sealed class GitHubPollingWorkerTests : TestBase
{
    private readonly CapturingDiscordDispatcher _discord;
    private readonly INotificationFilter _filter;
    private readonly INotificationStateTracker _stateTracker;

    public GitHubPollingWorkerTests()
    {
        this._discord = new CapturingDiscordDispatcher();
        this._filter = GetSubstitute<INotificationFilter>();
        this._stateTracker = GetSubstitute<INotificationStateTracker>();

        this._stateTracker.ShouldSkipPullRequestAsync(notification: Arg.Any<GitHubNotification>(), details: Arg.Any<PullRequestDetails>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        this._stateTracker.ShouldSkipIssueAsync(notification: Arg.Any<GitHubNotification>(), details: Arg.Any<IssueDetails>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
    }

    private static GitHubNotification BuildPrNotification(string reason)
    {
        return new GitHubNotification(
            Id: "1",
            Reason: reason,
            Subject: new NotificationSubject(Title: "Test PR", Url: new Uri("https://api.github.com/repos/owner/repo/pulls/42"), Type: "PullRequest"),
            Repository: new NotificationRepository(FullName: "owner/repo", Url: new Uri("https://github.com/owner/repo")),
            UpdatedAt: new DateTimeOffset(year: 2024, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero),
            Unread: true);
    }

    private static GitHubNotification BuildIssueNotification()
    {
        return new GitHubNotification(
            Id: "2",
            Reason: "mention",
            Subject: new NotificationSubject(Title: "Test Issue", Url: new Uri("https://api.github.com/repos/owner/repo/issues/10"), Type: "Issue"),
            Repository: new NotificationRepository(FullName: "owner/repo", Url: new Uri("https://github.com/owner/repo")),
            UpdatedAt: new DateTimeOffset(year: 2024, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero),
            Unread: true);
    }

    private static PullRequestDetails BuildPrDetails()
    {
        return new PullRequestDetails(
            Number: 42,
            Title: "Test PR",
            Body: null,
            Status: "Open",
            HtmlUrl: new Uri("https://github.com/owner/repo/pull/42"),
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: new Uri("https://github.com/owner/repo")),
            LastNotification: new LastNotification(Id: "1", Timestamp: new DateTimeOffset(year: 2024, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)),
            Assignees: [],
            Labels: [],
            Comments: [],
            Reviews: [],
            Runs: [],
            LinkedItems: []);
    }

    private static PullRequestDetails BuildClosedPrDetails()
    {
        return new PullRequestDetails(
            Number: 42,
            Title: "Test PR",
            Body: null,
            Status: "Closed",
            HtmlUrl: new Uri("https://github.com/owner/repo/pull/42"),
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: new Uri("https://github.com/owner/repo")),
            LastNotification: new LastNotification(Id: "1", Timestamp: new DateTimeOffset(year: 2024, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)),
            Assignees: [],
            Labels: [],
            Comments: [],
            Reviews: [],
            Runs: [],
            LinkedItems: []);
    }

    private static IssueDetails BuildOpenIssueDetails()
    {
        return new IssueDetails(
            Number: 10,
            Title: "Test Issue",
            Status: "Open",
            HtmlUrl: new Uri("https://github.com/owner/repo/issues/10"),
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: new Uri("https://github.com/owner/repo")),
            LastNotification: new LastNotification(Id: "2", Timestamp: new DateTimeOffset(year: 2024, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)));
    }

    private static IssueDetails BuildClosedIssueDetails()
    {
        return new IssueDetails(
            Number: 10,
            Title: "Test Issue",
            Status: "Closed",
            HtmlUrl: new Uri("https://github.com/owner/repo/issues/10"),
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: new Uri("https://github.com/owner/repo")),
            LastNotification: new LastNotification(Id: "2", Timestamp: new DateTimeOffset(year: 2024, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)));
    }

    private GitHubPollingWorker CreateWorker(INotificationPoller poller, IPullRequestDetailFetcher fetcher, IIssueDetailFetcher? issueFetcher = null)
    {
        return new GitHubPollingWorker(
            poller: poller,
            notificationFilter: this._filter,
            discordDispatcher: this._discord,
            pullRequestDetailFetcher: fetcher,
            issueDetailFetcher: issueFetcher ?? new FakeIssueFetcher(result: null),
            notificationStateTracker: this._stateTracker,
            options: Options.Create(new GitHubOptions { PollIntervalSeconds = 30 }),
            logger: this.GetTypedLogger<GitHubPollingWorker>());
    }

    private async Task<DiscordMessage> RunAndCaptureAsync(INotificationPoller poller, IPullRequestDetailFetcher fetcher, IIssueDetailFetcher? issueFetcher = null)
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        using GitHubPollingWorker worker = this.CreateWorker(poller: poller, fetcher: fetcher, issueFetcher: issueFetcher);
        await worker.StartAsync(token);
        DiscordMessage captured = await this._discord.Dispatched.WaitAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: token);
        await worker.StopAsync(token);

        return captured;
    }

    [Fact]
    public async Task PullRequestMessageContentContainsNumberReasonAndUrlAsync()
    {
        GitHubNotification notification = BuildPrNotification("mention");
        PullRequestDetails details = BuildPrDetails();

        this._filter.ShouldDispatch(notification)
                    .Returns(true);

        DiscordMessage captured = await this.RunAndCaptureAsync(
            poller: new FakePoller([notification]),
            fetcher: new FakeFetcher(details));

        Assert.Equal(expected: "[owner/repo] PR #42 (mention) https://github.com/owner/repo/pull/42", actual: captured.Content);
    }

    [Fact]
    public async Task PullRequestMessageFallsBackToBasicWhenFetcherReturnsNullAsync()
    {
        GitHubNotification notification = BuildPrNotification("mention");

        this._filter.ShouldDispatch(notification)
                    .Returns(true);

        DiscordMessage captured = await this.RunAndCaptureAsync(
            poller: new FakePoller([notification]),
            fetcher: new FakeFetcher(result: null));

        Assert.Equal(expected: "[owner/repo] PullRequest", actual: captured.Content);
    }

    [Fact]
    public async Task IssueNotificationUsesBasicMessageContentAsync()
    {
        GitHubNotification notification = BuildIssueNotification();

        this._filter.ShouldDispatch(notification)
                    .Returns(true);

        DiscordMessage captured = await this.RunAndCaptureAsync(
            poller: new FakePoller([notification]),
            fetcher: new FakeFetcher(result: null),
            issueFetcher: new FakeIssueFetcher(BuildOpenIssueDetails()));

        Assert.Equal(expected: "[owner/repo] Issue", actual: captured.Content);
    }

    [Fact]
    public async Task FilteredNotificationIsNotDispatchedAsync()
    {
        GitHubNotification notification = BuildPrNotification("mention");

        this._filter.ShouldDispatch(notification)
                    .Returns(false);

        CancellationToken token = TestContext.Current.CancellationToken;
        using GitHubPollingWorker worker = this.CreateWorker(poller: new FakePoller([notification]), fetcher: new FakeFetcher(result: null));
        await worker.StartAsync(token);
        await Task.Delay(millisecondsDelay: 200, cancellationToken: token);
        await worker.StopAsync(token);

        Assert.False(condition: this._discord.Dispatched.IsCompleted, userMessage: "Expected no message to be dispatched");
    }

    [Fact]
    public async Task ClosedPullRequestIsNotDispatchedWhenAlreadyTrackedAsClosedAsync()
    {
        GitHubNotification notification = BuildPrNotification("subscribed");
        PullRequestDetails details = BuildClosedPrDetails();

        this._filter.ShouldDispatch(notification)
                    .Returns(true);

        this._stateTracker.ShouldSkipPullRequestAsync(
                              notification: Arg.Any<GitHubNotification>(),
                              details: Arg.Is<PullRequestDetails>(d => d.Number == 42 && d.Status == "Closed"),
                              cancellationToken: Arg.Any<CancellationToken>())
                          .Returns(Task.FromResult(true));

        CancellationToken token = TestContext.Current.CancellationToken;
        using GitHubPollingWorker worker = this.CreateWorker(poller: new FakePoller([notification]), fetcher: new FakeFetcher(details));
        await worker.StartAsync(token);
        await Task.Delay(millisecondsDelay: 200, cancellationToken: token);
        await worker.StopAsync(token);

        Assert.False(condition: this._discord.Dispatched.IsCompleted, userMessage: "Expected no message to be dispatched for already-closed PR");
    }

    [Fact]
    public async Task ClosedIssueIsNotDispatchedWhenAlreadyTrackedAsClosedAsync()
    {
        GitHubNotification notification = BuildIssueNotification();
        IssueDetails details = BuildClosedIssueDetails();

        this._filter.ShouldDispatch(notification)
                    .Returns(true);

        this._stateTracker.ShouldSkipIssueAsync(
                              notification: Arg.Any<GitHubNotification>(),
                              details: Arg.Is<IssueDetails>(d => d.Number == 10 && d.Status == "Closed"),
                              cancellationToken: Arg.Any<CancellationToken>())
                          .Returns(Task.FromResult(true));

        CancellationToken token = TestContext.Current.CancellationToken;
        using GitHubPollingWorker worker = this.CreateWorker(poller: new FakePoller([notification]), fetcher: new FakeFetcher(result: null), issueFetcher: new FakeIssueFetcher(details));
        await worker.StartAsync(token);
        await Task.Delay(millisecondsDelay: 200, cancellationToken: token);
        await worker.StopAsync(token);

        Assert.False(condition: this._discord.Dispatched.IsCompleted, userMessage: "Expected no message to be dispatched for already-closed issue");
    }

    [Fact]
    public async Task ClosedPullRequestIsNeverDispatchedEvenOnFirstSeenAsync()
    {
        GitHubNotification notification = BuildPrNotification("subscribed");
        PullRequestDetails details = BuildClosedPrDetails();

        this._filter.ShouldDispatch(notification)
                    .Returns(true);

        // State tracker returns true (closed = always skip), so no dispatch should happen
        this._stateTracker.ShouldSkipPullRequestAsync(
                              notification: Arg.Any<GitHubNotification>(),
                              details: Arg.Any<PullRequestDetails>(),
                              cancellationToken: Arg.Any<CancellationToken>())
                          .Returns(Task.FromResult(true));

        CancellationToken token = TestContext.Current.CancellationToken;
        using GitHubPollingWorker worker = this.CreateWorker(poller: new FakePoller([notification]), fetcher: new FakeFetcher(details));
        await worker.StartAsync(token);
        await Task.Delay(millisecondsDelay: 200, cancellationToken: token);
        await worker.StopAsync(token);

        Assert.False(condition: this._discord.Dispatched.IsCompleted, userMessage: "Expected no message to be dispatched for first-seen closed PR");
    }

    private sealed class FakePoller : INotificationPoller
    {
        private readonly IReadOnlyList<GitHubNotification> _notifications;

        public FakePoller(IReadOnlyList<GitHubNotification> notifications)
        {
            this._notifications = notifications;
        }

        public ValueTask<IReadOnlyList<GitHubNotification>> PollAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(this._notifications);
        }
    }

    private sealed class FakeFetcher : IPullRequestDetailFetcher
    {
        private readonly PullRequestDetails? _result;

        public FakeFetcher(PullRequestDetails? result)
        {
            this._result = result;
        }

        public ValueTask<PullRequestDetails?> FetchAsync(GitHubNotification notification, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(this._result);
        }
    }

    private sealed class FakeIssueFetcher : IIssueDetailFetcher
    {
        private readonly IssueDetails? _result;

        public FakeIssueFetcher(IssueDetails? result)
        {
            this._result = result;
        }

        public ValueTask<IssueDetails?> FetchAsync(GitHubNotification notification, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(this._result);
        }
    }

    private sealed class CapturingDiscordDispatcher : IDiscordDispatcher
    {
        private readonly TaskCompletionSource<DiscordMessage> _tcs = new();

        public Task<DiscordMessage> Dispatched => this._tcs.Task;

        public ValueTask SendAsync(DiscordMessage message, CancellationToken cancellationToken)
        {
            this._tcs.TrySetResult(message);

            return ValueTask.CompletedTask;
        }
    }
}
