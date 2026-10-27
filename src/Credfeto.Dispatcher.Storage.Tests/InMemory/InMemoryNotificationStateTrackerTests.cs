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
            details: CreatePullRequestDetails(commitAuthors),
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

    private static PullRequestDetails CreatePullRequestDetails(IReadOnlyList<string> commitAuthors)
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
            LinkedItems: [],
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: repoUri),
            LastNotification: new LastNotification(Id: "notif-1", Timestamp: BaseTime),
            Author: null,
            CommitAuthors: commitAuthors,
            HasDetail: true
        );
    }
}
