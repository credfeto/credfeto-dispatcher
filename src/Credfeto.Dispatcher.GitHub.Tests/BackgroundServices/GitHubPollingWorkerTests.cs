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

    public GitHubPollingWorkerTests()
    {
        this._discord = new CapturingDiscordDispatcher();
        this._filter = GetSubstitute<INotificationFilter>();
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
            Status: "Open",
            HtmlUrl: new Uri("https://github.com/owner/repo/pull/42"),
            Assignees: [],
            Labels: [],
            CommentBody: null,
            CommentAuthor: null,
            CommentUrl: null,
            ReviewState: null,
            ReviewBody: null,
            ReviewAuthor: null,
            ReviewUrl: null,
            FailedRunName: null,
            FailedRunUrl: null);
    }

    private GitHubPollingWorker CreateWorker(INotificationPoller poller, IPullRequestDetailFetcher fetcher)
    {
        return new GitHubPollingWorker(
            poller: poller,
            notificationFilter: this._filter,
            discordDispatcher: this._discord,
            pullRequestDetailFetcher: fetcher,
            options: Options.Create(new GitHubOptions { PollIntervalSeconds = 30 }),
            logger: this.GetTypedLogger<GitHubPollingWorker>());
    }

    private async Task<DiscordMessage> RunAndCaptureAsync(INotificationPoller poller, IPullRequestDetailFetcher fetcher)
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        using GitHubPollingWorker worker = this.CreateWorker(poller: poller, fetcher: fetcher);
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
            fetcher: new FakeFetcher(result: null));

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
