using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.BackgroundServices;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.BackgroundServices;

public sealed class GitHubPollingWorkerTests : TestBase
{
    private readonly INotificationFilter _filter;
    private readonly INotificationStateTracker _stateTracker;

    public GitHubPollingWorkerTests()
    {
        this._filter = GetSubstitute<INotificationFilter>();
        this._stateTracker = GetSubstitute<INotificationStateTracker>();
    }

    private static GitHubNotification BuildPrNotification(string reason)
    {
        return new GitHubNotification(
            Id: "1",
            Reason: reason,
            Subject: new NotificationSubject(
                Title: "Test PR",
                Url: new Uri("https://api.github.com/repos/owner/repo/pulls/42"),
                Type: "PullRequest"
            ),
            Repository: new NotificationRepository(
                FullName: "owner/repo",
                Url: new Uri("https://github.com/owner/repo")
            ),
            UpdatedAt: new DateTimeOffset(
                year: 2024,
                month: 1,
                day: 1,
                hour: 0,
                minute: 0,
                second: 0,
                offset: TimeSpan.Zero
            ),
            Unread: true
        );
    }

    private static GitHubNotification BuildIssueNotification()
    {
        return new GitHubNotification(
            Id: "2",
            Reason: "mention",
            Subject: new NotificationSubject(
                Title: "Test Issue",
                Url: new Uri("https://api.github.com/repos/owner/repo/issues/10"),
                Type: "Issue"
            ),
            Repository: new NotificationRepository(
                FullName: "owner/repo",
                Url: new Uri("https://github.com/owner/repo")
            ),
            UpdatedAt: new DateTimeOffset(
                year: 2024,
                month: 1,
                day: 1,
                hour: 0,
                minute: 0,
                second: 0,
                offset: TimeSpan.Zero
            ),
            Unread: true
        );
    }

    private static ItemRepository BuildTestRepository()
    {
        return new ItemRepository(Owner: "owner", Name: "repo", Url: new Uri("https://github.com/owner/repo"));
    }

    private static LastNotification BuildTestLastNotification(string id)
    {
        return new LastNotification(
            Id: id,
            Timestamp: new DateTimeOffset(
                year: 2024,
                month: 1,
                day: 1,
                hour: 0,
                minute: 0,
                second: 0,
                offset: TimeSpan.Zero
            )
        );
    }

    private static PullRequestDetails BuildPrDetails(string status = "Open")
    {
        return new PullRequestDetails(
            Number: 42,
            Title: "Test PR",
            Status: status,
            HtmlUrl: new Uri("https://github.com/owner/repo/pull/42"),
            Assignees: [],
            Labels: [],
            Body: null,
            Comments: [],
            Reviews: [],
            Runs: [],
            LinkedItems: [],
            Repository: BuildTestRepository(),
            LastNotification: BuildTestLastNotification("1"),
            Author: null
        );
    }

    private static IssueDetails BuildIssueDetails(string status = "Open")
    {
        return new IssueDetails(
            Number: 10,
            Title: "Test Issue",
            Status: status,
            HtmlUrl: new Uri("https://github.com/owner/repo/issues/10"),
            Assignees: [],
            Labels: [],
            LinkedPullRequestUrl: null,
            Repository: BuildTestRepository(),
            LastNotification: BuildTestLastNotification("2")
        );
    }

    private GitHubPollingWorker CreateWorker(
        INotificationPoller poller,
        IPullRequestDetailFetcher fetcher,
        IIssueDetailFetcher? issueFetcher = null,
        IModifiedIssueMentionPoller? mentionPoller = null
    )
    {
        return new GitHubPollingWorker(
            poller: poller,
            modifiedIssueMentionPoller: mentionPoller ?? new FakeMentionPoller(),
            notificationFilter: this._filter,
            pullRequestDetailFetcher: fetcher,
            issueDetailFetcher: issueFetcher ?? new FakeIssueFetcher(result: null),
            notificationStateTracker: this._stateTracker,
            options: Options.Create(new GitHubOptions { PollIntervalSeconds = 30 }),
            logger: this.GetTypedLogger<GitHubPollingWorker>()
        );
    }

    private async Task RunWorkerAsync(
        INotificationPoller poller,
        IPullRequestDetailFetcher fetcher,
        IIssueDetailFetcher? issueFetcher = null,
        IModifiedIssueMentionPoller? mentionPoller = null
    )
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        using GitHubPollingWorker worker = this.CreateWorker(
            poller: poller,
            fetcher: fetcher,
            issueFetcher: issueFetcher,
            mentionPoller: mentionPoller
        );
        await worker.StartAsync(token);
        await Task.Delay(millisecondsDelay: 200, cancellationToken: token);
        await worker.StopAsync(token);
    }

    [Fact]
    public async Task PullRequestWhenProcessedUpdatesStateAsync()
    {
        GitHubNotification notification = BuildPrNotification("mention");
        PullRequestDetails details = BuildPrDetails();

        this._filter.ShouldProcess(notification).Returns(true);

        await this.RunWorkerAsync(poller: new FakePoller([notification]), fetcher: new FakeFetcher(details));

        await this
            ._stateTracker.Received(1)
            .UpdateStateAsync(
                notification: notification,
                details: details,
                priority: WorkPriority.UNKNOWN,
                isOnHold: false,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PullRequestWhenFetcherReturnsNullDoesNotUpdateStateAsync()
    {
        GitHubNotification notification = BuildPrNotification("mention");

        this._filter.ShouldProcess(notification).Returns(true);

        await this.RunWorkerAsync(poller: new FakePoller([notification]), fetcher: new FakeFetcher(result: null));

        Assert.Empty(this._stateTracker.ReceivedCalls());
    }

    [Fact]
    public async Task IssueWhenProcessedUpdatesStateAsync()
    {
        GitHubNotification notification = BuildIssueNotification();
        IssueDetails details = BuildIssueDetails();

        this._filter.ShouldProcess(notification).Returns(true);

        await this.RunWorkerAsync(
            poller: new FakePoller([notification]),
            fetcher: new FakeFetcher(result: null),
            issueFetcher: new FakeIssueFetcher(details)
        );

        await this
            ._stateTracker.Received(1)
            .UpdateStateAsync(
                notification: notification,
                details: details,
                priority: WorkPriority.UNKNOWN,
                isOnHold: false,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task FilteredNotificationDoesNotUpdateStateAsync()
    {
        GitHubNotification notification = BuildPrNotification("mention");

        this._filter.ShouldProcess(notification).Returns(false);

        await this.RunWorkerAsync(poller: new FakePoller([notification]), fetcher: new FakeFetcher(result: null));

        Assert.Empty(this._stateTracker.ReceivedCalls());
    }

    [Fact]
    public async Task ClosedPullRequestWhenProcessedUpdatesStateAsync()
    {
        GitHubNotification notification = BuildPrNotification("subscribed");
        PullRequestDetails details = BuildPrDetails(status: "Closed");

        this._filter.ShouldProcess(notification).Returns(true);

        await this.RunWorkerAsync(poller: new FakePoller([notification]), fetcher: new FakeFetcher(details));

        await this
            ._stateTracker.Received(1)
            .UpdateStateAsync(
                notification: notification,
                details: details,
                priority: WorkPriority.UNKNOWN,
                isOnHold: false,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ClosedIssueWhenProcessedUpdatesStateAsync()
    {
        GitHubNotification notification = BuildIssueNotification();
        IssueDetails details = BuildIssueDetails(status: "Closed");

        this._filter.ShouldProcess(notification).Returns(true);

        await this.RunWorkerAsync(
            poller: new FakePoller([notification]),
            fetcher: new FakeFetcher(result: null),
            issueFetcher: new FakeIssueFetcher(details)
        );

        await this
            ._stateTracker.Received(1)
            .UpdateStateAsync(
                notification: notification,
                details: details,
                priority: WorkPriority.UNKNOWN,
                isOnHold: false,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task MentionNotificationsWhenEnabledUpdatesStateAsync()
    {
        GitHubNotification notification = BuildIssueNotification();
        IssueDetails details = BuildIssueDetails();

        this._filter.ShouldProcess(notification).Returns(true);

        await this.RunWorkerAsync(
            poller: new FakePoller([]),
            fetcher: new FakeFetcher(result: null),
            issueFetcher: new FakeIssueFetcher(details),
            mentionPoller: new FakeMentionPoller([notification])
        );

        await this
            ._stateTracker.Received(1)
            .UpdateStateAsync(
                notification: notification,
                details: details,
                priority: WorkPriority.UNKNOWN,
                isOnHold: false,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    private sealed class FakeMentionPoller : IModifiedIssueMentionPoller
    {
        private readonly IReadOnlyList<GitHubNotification> _notifications;

        public FakeMentionPoller(IReadOnlyList<GitHubNotification>? notifications = null)
        {
            this._notifications = notifications ?? [];
        }

        public ValueTask<IReadOnlyList<GitHubNotification>> PollAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(this._notifications);
        }
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

        public ValueTask<PullRequestDetails?> FetchAsync(
            GitHubNotification notification,
            CancellationToken cancellationToken
        )
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
}
