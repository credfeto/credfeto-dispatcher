using System;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using FunFair.Test.Common;
using FunFair.Test.Common.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Services;

public sealed class NotificationStateTrackerTests : TestBase
{
    private static readonly DateTimeOffset BaseTime = MockDateTimeSources.Past.GetUtcNow();

    private readonly TestDatabaseStub _database;
    private readonly INotificationStateTracker _tracker;

    public NotificationStateTrackerTests()
    {
        this._database = new TestDatabaseStub();
        this._tracker = new NotificationStateTracker(this._database);
    }

    private static GitHubNotification CreateNotification(string repo = "owner/repo")
    {
        return new GitHubNotification(
            Id: "notif-1",
            Reason: "review_requested",
            Subject: new NotificationSubject(
                Title: "Test PR",
                Url: new Uri("https://api.github.com/repos/owner/repo/pulls/1"),
                Type: "PullRequest"
            ),
            Repository: new NotificationRepository(
                FullName: repo,
                Url: new Uri("https://api.github.com/repos/owner/repo")
            ),
            UpdatedAt: BaseTime,
            Unread: true
        );
    }

    private static PullRequestDetails CreatePullRequestDetails(string status)
    {
        Uri repoUri = new("https://github.com/owner/repo");

        return new PullRequestDetails(
            Number: 1,
            Title: "Test PR",
            Status: status,
            HtmlUrl: new Uri("https://github.com/owner/repo/pull/1"),
            Assignees: [],
            Labels: [],
            Body: null,
            Comments: [],
            Reviews: [],
            Runs: [],
            LinkedItems: [],
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: repoUri),
            LastNotification: new LastNotification(Id: "notif-1", Timestamp: BaseTime),
            Author: null,
            CommitAuthors: []
        );
    }

    private static PullRequestDetails CreatePullRequestDetailsWithLinkedIssue(string status, int linkedIssueNumber)
    {
        Uri repoUri = new("https://github.com/owner/repo");

        return new PullRequestDetails(
            Number: 1,
            Title: "Test PR",
            Status: status,
            HtmlUrl: new Uri("https://github.com/owner/repo/pull/1"),
            Assignees: [],
            Labels: [],
            Body: null,
            Comments: [],
            Reviews: [],
            Runs: [],
            LinkedItems: [new LinkedItem(Number: linkedIssueNumber, Labels: [], Assignees: [])],
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: repoUri),
            LastNotification: new LastNotification(Id: "notif-1", Timestamp: BaseTime),
            Author: null,
            CommitAuthors: []
        );
    }

    private static IssueDetails CreateIssueDetails(string status)
    {
        Uri repoUri = new("https://github.com/owner/repo");

        return new IssueDetails(
            Number: 1,
            Title: "Test Issue",
            Status: status,
            HtmlUrl: new Uri("https://github.com/owner/repo/issues/1"),
            Assignees: [],
            Labels: [],
            LinkedPullRequestUrl: null,
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: repoUri),
            LastNotification: new LastNotification(Id: "notif-1", Timestamp: BaseTime)
        );
    }

    [Fact]
    public async Task UpdateStateAsyncForPullRequestCallsDatabaseAsync()
    {
        PullRequestDetails details = CreatePullRequestDetails("Open");

        await this._tracker.UpdateStateAsync(
            notification: CreateNotification(),
            details: details,
            priority: WorkPriority.MEDIUM,
            isOnHold: false,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 1, actual: this._database.VoidExecuteCallCount);
    }

    [Fact]
    public async Task UpdateStateAsyncForClosedPullRequestCallsDatabaseAsync()
    {
        PullRequestDetails details = CreatePullRequestDetails("Closed");

        await this._tracker.UpdateStateAsync(
            notification: CreateNotification(),
            details: details,
            priority: WorkPriority.MEDIUM,
            isOnHold: false,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 1, actual: this._database.VoidExecuteCallCount);
    }

    [Fact]
    public async Task UpdateStateAsyncForPullRequestWithLinkedIssueUpdatesIssueLinkAsync()
    {
        PullRequestDetails details = CreatePullRequestDetailsWithLinkedIssue(status: "Open", linkedIssueNumber: 13);

        await this._tracker.UpdateStateAsync(
            notification: CreateNotification(),
            details: details,
            priority: WorkPriority.MEDIUM,
            isOnHold: false,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: this._database.VoidExecuteCallCount);
    }

    [Fact]
    public async Task UpdateStateAsyncForIssueCallsDatabaseAsync()
    {
        IssueDetails details = CreateIssueDetails("Open");

        await this._tracker.UpdateStateAsync(
            notification: CreateNotification(),
            details: details,
            priority: WorkPriority.MEDIUM,
            isOnHold: false,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 1, actual: this._database.VoidExecuteCallCount);
    }

    [Fact]
    public async Task UpdateStateAsyncForClosedIssueCallsDatabaseAsync()
    {
        IssueDetails details = CreateIssueDetails("Closed");

        await this._tracker.UpdateStateAsync(
            notification: CreateNotification(),
            details: details,
            priority: WorkPriority.MEDIUM,
            isOnHold: false,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 1, actual: this._database.VoidExecuteCallCount);
    }
}
