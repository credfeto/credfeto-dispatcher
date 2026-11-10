using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database.Rows;
using Credfeto.Dispatcher.Storage.InMemory;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.InMemory;

public sealed class InMemoryNotificationStateTrackerTests : TestBase
{
    private const string REPOSITORY = "owner/repo";

    private static readonly DateTimeOffset BaseTime = MockDateTimeSources.Past.GetUtcNow();

    private readonly InMemoryDispatcherStore _store;
    private readonly INotificationStateTracker _tracker;

    public InMemoryNotificationStateTrackerTests()
    {
        this._store = new InMemoryDispatcherStore(MockDateTimeSources.Past);
        this._tracker = new InMemoryNotificationStateTracker(this._store);
    }

    [Fact]
    public async Task UpdateStateAsyncStoresNonEmptyCommitAuthorsAsync()
    {
        await this.TrackPullRequestAsync(["someone-else"]);
        this.UpsertOpenIssue();

        Assert.Single(this.GetActiveIssues("octocat"));
        Assert.Empty(this.GetActiveIssues("someone-else"));
    }

    [Fact]
    public async Task UpdateStateAsyncReplacesStoredCommitAuthorsWithNonEmptyListAsync()
    {
        await this.TrackPullRequestAsync(["someone-else"]);
        await this.TrackPullRequestAsync(["octocat"]);
        this.UpsertOpenIssue();

        Assert.Empty(this.GetActiveIssues("octocat"));
        Assert.Single(this.GetActiveIssues("someone-else"));
    }

    [Fact]
    public async Task UpdateStateAsyncWithEmptyCommitAuthorsKeepsStoredCommitAuthorsAsync()
    {
        await this.TrackPullRequestAsync(["someone-else"]);
        await this.TrackPullRequestAsync([]);
        this.UpsertOpenIssue();

        Assert.Single(this.GetActiveIssues("octocat"));
    }

    [Fact]
    public async Task UpdateStateAsyncWithOnlyEmptyCommitAuthorsTreatsAuthorsAsUnknownAsync()
    {
        await this.TrackPullRequestAsync([]);
        this.UpsertOpenIssue();

        Assert.Empty(this.GetActiveIssues("octocat"));
    }

    [Fact]
    public async Task UpdateStateAsyncLinksEachDistinctLinkedIssueToThePullRequestAsync()
    {
        this.UpsertOpenIssue();

        await this._tracker.UpdateStateAsync(
            notification: CreateNotification(),
            details: CreatePullRequestDetails(
                commitAuthors: [],
                linkedItems: [CreateLinkedItem(20), CreateLinkedItem(20)]
            ),
            priority: WorkPriority.MEDIUM,
            isOnHold: false,
            isAdopted: null,
            cancellationToken: this.CancellationToken()
        );

        IssueRow issue = Assert.Single(this._store.ExportSnapshot().Issues);
        Assert.Equal(expected: 1, actual: issue.LinkedPrNumber);
    }

    [Fact]
    public async Task UpdateStateAsyncForIssueStoresTheIssueWithItsLinkedPullRequestAsync()
    {
        await this._tracker.UpdateStateAsync(
            notification: CreateNotification(),
            details: CreateIssueDetails(new Uri("https://github.com/owner/repo/pull/7")),
            priority: WorkPriority.HIGH,
            isOnHold: true,
            cancellationToken: this.CancellationToken()
        );

        IssueRow issue = Assert.Single(this._store.ExportSnapshot().Issues);
        Assert.Equal(expected: REPOSITORY, actual: issue.Repository);
        Assert.Equal(expected: 20, actual: issue.Id);
        Assert.Equal(expected: "Open", actual: issue.Status);
        Assert.Equal(expected: (int)WorkPriority.HIGH, actual: issue.Priority);
        Assert.True(condition: issue.IsOnHold, userMessage: "Issue should be stored as on hold");
        Assert.Equal(expected: 7, actual: issue.LinkedPrNumber);
    }

    [Fact]
    public async Task UpdateStateAsyncForIssueWithoutLinkedPullRequestStoresNoLinkAsync()
    {
        await this._tracker.UpdateStateAsync(
            notification: CreateNotification(),
            details: CreateIssueDetails(null),
            priority: WorkPriority.MEDIUM,
            isOnHold: false,
            cancellationToken: this.CancellationToken()
        );

        IssueRow issue = Assert.Single(this._store.ExportSnapshot().Issues);
        Assert.Null(issue.LinkedPrNumber);
    }

    private static LinkedItem CreateLinkedItem(int number)
    {
        return new LinkedItem(Number: number, Labels: [], Assignees: []);
    }

    private static IssueDetails CreateIssueDetails(Uri? linkedPullRequestUrl)
    {
        return new IssueDetails(
            Number: 20,
            Title: "Test issue",
            Status: "Open",
            HtmlUrl: new Uri("https://github.com/owner/repo/issues/20"),
            Assignees: ["octocat"],
            Labels: [],
            LinkedPullRequestUrl: linkedPullRequestUrl,
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: new Uri("https://github.com/owner/repo")),
            LastNotification: new LastNotification(Id: "notif-1", Timestamp: BaseTime)
        );
    }

    private IReadOnlyList<IssueRow> GetActiveIssues(string user)
    {
        (_, IReadOnlyList<IssueRow> issues) = this._store.GetActiveWorkItems(user: user);

        return issues;
    }

    private void UpsertOpenIssue()
    {
        this._store.UpsertIssue(
            repository: REPOSITORY,
            id: 20,
            status: "Open",
            priority: (int)WorkPriority.MEDIUM,
            isOnHold: false,
            linkedPrNumber: null,
            assignees: null
        );
    }

    private ValueTask TrackPullRequestAsync(IReadOnlyList<string> commitAuthors)
    {
        return this._tracker.UpdateStateAsync(
            notification: CreateNotification(),
            details: CreatePullRequestDetails(commitAuthors: commitAuthors, linkedItems: []),
            priority: WorkPriority.MEDIUM,
            isOnHold: false,
            isAdopted: null,
            cancellationToken: this.CancellationToken()
        );
    }

    private static GitHubNotification CreateNotification()
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
                FullName: REPOSITORY,
                Url: new Uri("https://api.github.com/repos/owner/repo")
            ),
            UpdatedAt: BaseTime,
            Unread: true
        );
    }

    private static PullRequestDetails CreatePullRequestDetails(
        IReadOnlyList<string> commitAuthors,
        IReadOnlyList<LinkedItem> linkedItems
    )
    {
        Uri repoUri = new("https://github.com/owner/repo");

        return new PullRequestDetails(
            Number: 1,
            Title: "Test PR",
            Status: "Open",
            HtmlUrl: new Uri("https://github.com/owner/repo/pull/1"),
            Assignees: [],
            Labels: [],
            Body: null,
            Comments: [],
            Reviews: [],
            Runs: [],
            LinkedItems: linkedItems,
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: repoUri),
            LastNotification: new LastNotification(Id: "notif-1", Timestamp: BaseTime),
            Author: null,
            CommitAuthors: commitAuthors,
            HasDetail: true
        );
    }
}
