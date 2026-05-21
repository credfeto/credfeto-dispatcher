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
        this._tracker = new NotificationStateTracker(this._database, MockDateTimeSources.Past);
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
            HeadBranchName: null
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
    public async Task ShouldSkip_ReturnsFalse_ForOpenPullRequestAsync()
    {
        PullRequestDetails details = CreatePullRequestDetails("Open");

        bool result = await this._tracker.ShouldSkipAsync(
            notification: CreateNotification(),
            details: details,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result, userMessage: "Should not skip an open pull request");
    }

    [Fact]
    public async Task ShouldSkip_ReturnsTrue_ForClosedPullRequestAsync()
    {
        PullRequestDetails details = CreatePullRequestDetails("Closed");

        bool result = await this._tracker.ShouldSkipAsync(
            notification: CreateNotification(),
            details: details,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result, userMessage: "Should skip a closed pull request");
    }

    [Fact]
    public async Task UpdateStateAsync_ForPullRequest_CallsDatabaseAsync()
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
    public async Task UpdateStateAsync_ForIssue_CallsDatabaseAsync()
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
}
