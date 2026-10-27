using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using Credfeto.Dispatcher.GitHub.Tests.Helpers;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class RepoEventPollerTests : TestBase
{
    private const string REPO = "owner/repo";

    private const string EMPTY_JSON = "[]";

    private const string PR_OPENED_EVENT_WITH_AI_WORK_LABEL_JSON = """
        [{
          "id": "100",
          "type": "PullRequestEvent",
          "repo": {"name": "owner/repo"},
          "payload": {
            "action": "opened",
            "pull_request": {
              "number": 40,
              "title": "AI Work PR",
              "state": "open",
              "draft": false,
              "html_url": "https://github.com/owner/repo/pull/40",
              "assignees": [],
              "labels": [{"name": "AI-Work"}],
              "head": {"sha": "aaa111"},
              "user": {"login": "dnyw4l3n13"}
            }
          },
          "created_at": "2024-01-01T00:00:00Z"
        }]
        """;

    private const string PR_OPENED_EVENT_WITH_UNRELATED_LABEL_JSON = """
        [{
          "id": "101",
          "type": "PullRequestEvent",
          "repo": {"name": "owner/repo"},
          "payload": {
            "action": "opened",
            "pull_request": {
              "number": 41,
              "title": "Unrelated PR",
              "state": "open",
              "draft": false,
              "html_url": "https://github.com/owner/repo/pull/41",
              "assignees": [],
              "labels": [{"name": "bug"}],
              "head": {"sha": "bbb222"},
              "user": {"login": "dnyw4l3n13"}
            }
          },
          "created_at": "2024-01-01T00:00:00Z"
        }]
        """;

    private const string PR_OPENED_EVENT_RESOLVING_OWN_ISSUE_JSON = """
        [{
          "id": "102",
          "type": "PullRequestEvent",
          "repo": {"name": "owner/repo"},
          "payload": {
            "action": "opened",
            "pull_request": {
              "number": 42,
              "title": "Resolves own issue",
              "state": "open",
              "draft": false,
              "html_url": "https://github.com/owner/repo/pull/42",
              "assignees": [],
              "labels": [{"name": "auto-pr"}],
              "head": {"sha": "ccc333"},
              "user": {"login": "dnyw4l3n13"}
            }
          },
          "created_at": "2024-01-01T00:00:00Z"
        }]
        """;

    private const string PR_CLOSED_THEN_OPENED_SAME_PR_JSON = """
        [{
          "id": "101",
          "type": "PullRequestEvent",
          "repo": {"name": "owner/repo"},
          "payload": {
            "action": "closed",
            "pull_request": {
              "number": 40,
              "title": "AI Work PR",
              "state": "closed",
              "draft": false,
              "html_url": "https://github.com/owner/repo/pull/40",
              "assignees": [],
              "labels": [{"name": "AI-Work"}],
              "head": {"sha": "aaa111"},
              "user": {"login": "dnyw4l3n13"}
            }
          },
          "created_at": "2024-01-01T00:05:00Z"
        },
        {
          "id": "100",
          "type": "PullRequestEvent",
          "repo": {"name": "owner/repo"},
          "payload": {
            "action": "opened",
            "pull_request": {
              "number": 40,
              "title": "AI Work PR",
              "state": "open",
              "draft": false,
              "html_url": "https://github.com/owner/repo/pull/40",
              "assignees": [],
              "labels": [{"name": "AI-Work"}],
              "head": {"sha": "aaa111"},
              "user": {"login": "dnyw4l3n13"}
            }
          },
          "created_at": "2024-01-01T00:00:00Z"
        }]
        """;

    private const string ISSUE_OPENED_EVENT_WITH_AI_WORK_LABEL_JSON = """
        [{
          "id": "200",
          "type": "IssuesEvent",
          "repo": {"name": "owner/repo"},
          "payload": {
            "action": "opened",
            "issue": {
              "number": 50,
              "title": "AI Work Issue",
              "state": "open",
              "html_url": "https://github.com/owner/repo/issues/50",
              "assignees": [],
              "labels": [{"name": "AI-Work"}],
              "pull_request": null
            }
          },
          "created_at": "2024-01-01T00:00:00Z"
        }]
        """;

    private const string ISSUE_OPENED_EVENT_WITH_UNRELATED_LABEL_JSON = """
        [{
          "id": "201",
          "type": "IssuesEvent",
          "repo": {"name": "owner/repo"},
          "payload": {
            "action": "opened",
            "issue": {
              "number": 51,
              "title": "Unrelated Issue",
              "state": "open",
              "html_url": "https://github.com/owner/repo/issues/51",
              "assignees": [],
              "labels": [{"name": "bug"}],
              "pull_request": null
            }
          },
          "created_at": "2024-01-01T00:00:00Z"
        }]
        """;

    private const string REPO_LAST_ID_KEY = "events.repo.lastid:owner/repo";

    private const string REPO_SEEN_IDS_KEY = "events.repo.seen:owner/repo";

    private readonly IActiveRepoTracker _activeRepoTracker;
    private readonly IETagStore _eTagStore;
    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly INotificationStateTracker _notificationStateTracker;

    public RepoEventPollerTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        this._activeRepoTracker = GetSubstitute<IActiveRepoTracker>();
        this._eTagStore = GetSubstitute<IETagStore>();
        this._notificationStateTracker = GetSubstitute<INotificationStateTracker>();

        this._activeRepoTracker.GetActiveReposAsync(Arg.Any<CancellationToken>()).Returns([REPO]);
        this._eTagStore.GetETagAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
    }

    private RepoEventPoller CreatePoller(
        GitHubOptions? options = null,
        IPullRequestDetailFetcher? pullRequestDetailFetcher = null
    )
    {
        GitHubRepoHelper helper = new(
            httpClientFactory: this._httpClientFactory,
            logger: this.GetTypedLogger<GitHubRepoHelper>()
        );

        return new RepoEventPoller(
            helper: helper,
            activeRepoTracker: this._activeRepoTracker,
            eTagStore: this._eTagStore,
            notificationStateTracker: this._notificationStateTracker,
            pullRequestDetailFetcher: pullRequestDetailFetcher ?? new FakePullRequestDetailFetcher(result: null),
            options: Options.Create(options ?? new GitHubOptions()),
            logger: this.GetTypedLogger<RepoEventPoller>()
        );
    }

    private static HttpClient CreateClient(HttpStatusCode statusCode, string? content = null)
    {
        FixedResponseHandler? handler = new(statusCode: statusCode, content: content);

        try
        {
            HttpClient client = new(handler: handler, disposeHandler: true)
            {
                BaseAddress = new Uri("https://api.github.com/"),
            };
            handler = null;

            return client;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    private sealed class FakePullRequestDetailFetcher : IPullRequestDetailFetcher
    {
        private readonly PullRequestDetails? _result;

        public FakePullRequestDetailFetcher(PullRequestDetails? result)
        {
            this._result = result;
        }

        public int CallCount { get; private set; }

        public ValueTask<PullRequestDetails?> FetchAsync(
            GitHubNotification notification,
            CancellationToken cancellationToken
        )
        {
            this.CallCount++;

            return ValueTask.FromResult(this._result);
        }
    }

    private PullRequestDetails SinglePullRequestUpdate()
    {
        return Assert.Single(
            this._notificationStateTracker.ReceivedCalls()
                .Select(call => call.GetArguments()[1])
                .OfType<PullRequestDetails>()
        );
    }

    private static PullRequestDetails BuildEnrichedDetails(
        string? author,
        IReadOnlyList<string> commitAuthors,
        IReadOnlyList<LinkedItem> linkedItems
    )
    {
        return new PullRequestDetails(
            Number: 42,
            Title: "Resolves own issue",
            Status: "Open",
            HtmlUrl: new Uri("https://github.com/owner/repo/pull/42"),
            Assignees: [],
            Labels: [],
            Body: null,
            Comments: [],
            Reviews: [],
            Runs: [],
            LinkedItems: linkedItems,
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: new Uri("https://github.com/owner/repo")),
            LastNotification: new LastNotification(Id: "event:owner/repo:pr:42", Timestamp: DateTimeOffset.MinValue),
            Author: author,
            CommitAuthors: commitAuthors,
            HasDetail: true
        );
    }

    private static string BuildIssueEventJson(string id, string number)
    {
        return $$"""
            {
              "id": "{{id}}",
              "type": "IssuesEvent",
              "repo": {"name": "owner/repo"},
              "payload": {
                "action": "unlabeled",
                "issue": {
                  "number": {{number}},
                  "title": "AI Work Issue {{number}}",
                  "state": "open",
                  "html_url": "https://github.com/owner/repo/issues/{{number}}",
                  "assignees": [],
                  "labels": [{"name": "AI-Work"}],
                  "pull_request": null
                }
              },
              "created_at": "2024-01-01T00:00:00Z"
            }
            """;
    }

    private static string BuildFeedJson(IReadOnlyList<string> events)
    {
        return "[" + string.Join(separator: ',', values: events) + "]";
    }

    private List<int> IssueNumbersUpdatedInCallOrder()
    {
        return
        [
            .. this
                ._notificationStateTracker.ReceivedCalls()
                .Select(call => call.GetArguments()[1])
                .OfType<IssueDetails>()
                .Select(details => details.Number),
        ];
    }

    private void GivenStoredRepoFeedState(string lastId, string seenIds)
    {
        this._eTagStore.GetETagAsync(REPO_LAST_ID_KEY, Arg.Any<CancellationToken>()).Returns(lastId);
        this._eTagStore.GetETagAsync(REPO_SEEN_IDS_KEY, Arg.Any<CancellationToken>()).Returns(seenIds);
    }

    [Fact]
    public async Task PollAsync_WithPrEventMatchingLabelFilter_CallsUpdateStateAsync()
    {
        using HttpClient repoFeedClient = CreateClient(HttpStatusCode.OK, PR_OPENED_EVENT_WITH_AI_WORK_LABEL_JSON);
        using HttpClient ownerFeedClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options);

        await poller.PollAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 40 && !d.HasDetail),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                isAdopted: Arg.Any<bool?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PollAsync_WithPrClosedThenOpenedEventsInSameBatch_AppliesEventsOldestFirstSoClosedStateWinsAsync()
    {
        using HttpClient repoFeedClient = CreateClient(HttpStatusCode.OK, PR_CLOSED_THEN_OPENED_SAME_PR_JSON);
        using HttpClient ownerFeedClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options);

        await poller.PollAsync(this.CancellationToken());

        List<string> statusesInCallOrder =
        [
            .. this
                ._notificationStateTracker.ReceivedCalls()
                .Select(call => call.GetArguments()[1])
                .OfType<PullRequestDetails>()
                .Select(details => details.Status),
        ];

        Assert.Equal(expected: ["Open", "Closed"], actual: statusesInCallOrder);
    }

    [Fact]
    public async Task PollAsync_WithPrEventFailingLabelFilterAndNoEnrichment_DoesNotCallUpdateStateAsync()
    {
        using HttpClient repoFeedClient = CreateClient(HttpStatusCode.OK, PR_OPENED_EVENT_WITH_UNRELATED_LABEL_JSON);
        using HttpClient ownerFeedClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options);

        await poller.PollAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                isAdopted: Arg.Any<bool?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PollAsync_WithPrEventFailingLabelFilterButResolvingOwnAssignedIssue_CallsUpdateWithElevatedPriorityAsync()
    {
        using HttpClient repoFeedClient = CreateClient(HttpStatusCode.OK, PR_OPENED_EVENT_RESOLVING_OWN_ISSUE_JSON);
        using HttpClient ownerFeedClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        LinkedItem linkedIssue = new(Number: 442, Labels: ["AI-Work", "urgent"], Assignees: ["dnyw4l3n13"]);
        PullRequestDetails enriched = BuildEnrichedDetails(
            author: "dnyw4l3n13",
            commitAuthors: ["dnyw4l3n13"],
            linkedItems: [linkedIssue]
        );
        FakePullRequestDetailFetcher fetcher = new(enriched);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options: options, pullRequestDetailFetcher: fetcher);

        await poller.PollAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 42),
                priority: WorkPriority.URGENT,
                isOnHold: Arg.Any<bool>(),
                isAdopted: Arg.Any<bool?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PollAsync_WithPrEventFailingLabelFilterButResolvingOwnAssignedIssue_StoresEnrichedCommitAuthorsAsync()
    {
        using HttpClient repoFeedClient = CreateClient(HttpStatusCode.OK, PR_OPENED_EVENT_RESOLVING_OWN_ISSUE_JSON);
        using HttpClient ownerFeedClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        LinkedItem linkedIssue = new(Number: 442, Labels: ["AI-Work", "urgent"], Assignees: ["dnyw4l3n13"]);
        PullRequestDetails enriched = BuildEnrichedDetails(
            author: "dnyw4l3n13",
            commitAuthors: ["dnyw4l3n13"],
            linkedItems: [linkedIssue]
        );
        FakePullRequestDetailFetcher fetcher = new(enriched);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options: options, pullRequestDetailFetcher: fetcher);

        await poller.PollAsync(this.CancellationToken());

        PullRequestDetails details = this.SinglePullRequestUpdate();
        Assert.Equal(expected: 42, actual: details.Number);
        Assert.Equal(expected: ["dnyw4l3n13"], actual: details.CommitAuthors);
    }

    [Fact]
    public async Task PollAsync_WithPrEventMatchingLabelFilter_DoesNotEnrichAndStoresUnknownCommitAuthorsAsync()
    {
        using HttpClient repoFeedClient = CreateClient(HttpStatusCode.OK, PR_OPENED_EVENT_WITH_AI_WORK_LABEL_JSON);
        using HttpClient ownerFeedClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        PullRequestDetails enriched = BuildEnrichedDetails(
            author: "dnyw4l3n13",
            commitAuthors: ["dnyw4l3n13"],
            linkedItems: []
        );
        FakePullRequestDetailFetcher fetcher = new(enriched);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options: options, pullRequestDetailFetcher: fetcher);

        await poller.PollAsync(this.CancellationToken());

        PullRequestDetails details = this.SinglePullRequestUpdate();
        Assert.Equal(expected: 40, actual: details.Number);
        Assert.Empty(details.CommitAuthors);
        Assert.Equal(expected: 0, actual: fetcher.CallCount);
    }

    [Fact]
    public async Task PollAsync_WithPrEventFailingLabelFilterAndLinkedIssueAssignedToSomeoneElse_DoesNotCallUpdateStateAsync()
    {
        using HttpClient repoFeedClient = CreateClient(HttpStatusCode.OK, PR_OPENED_EVENT_RESOLVING_OWN_ISSUE_JSON);
        using HttpClient ownerFeedClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        LinkedItem linkedIssue = new(Number: 442, Labels: ["AI-Work"], Assignees: ["someone-else"]);
        PullRequestDetails enriched = BuildEnrichedDetails(
            author: "dnyw4l3n13",
            commitAuthors: ["dnyw4l3n13"],
            linkedItems: [linkedIssue]
        );
        FakePullRequestDetailFetcher fetcher = new(enriched);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options: options, pullRequestDetailFetcher: fetcher);

        await poller.PollAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                isAdopted: Arg.Any<bool?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PollAsync_WithIssueEventMatchingLabelFilter_CallsUpdateStateAsync()
    {
        using HttpClient repoFeedClient = CreateClient(HttpStatusCode.OK, ISSUE_OPENED_EVENT_WITH_AI_WORK_LABEL_JSON);
        using HttpClient ownerFeedClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options);

        await poller.PollAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<IssueDetails>(d => d.Number == 50),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PollAsync_WithIssueEventFailingLabelFilter_DoesNotCallUpdateStateAsync()
    {
        using HttpClient repoFeedClient = CreateClient(HttpStatusCode.OK, ISSUE_OPENED_EVENT_WITH_UNRELATED_LABEL_JSON);
        using HttpClient ownerFeedClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options);

        await poller.PollAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<IssueDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PollAsync_WithStoredETagForRepoFeed_SendsIfNoneMatchHeaderAsync()
    {
        const string storedETag = "\"stored-etag\"";
        this._eTagStore.GetETagAsync("events.repo.etag:owner/repo", Arg.Any<CancellationToken>()).Returns(storedETag);

        (HttpClient repoFeedClient, FixedResponseHandler repoHandler) = HttpClientTestFactory.CreateWithHandler(
            statusCode: HttpStatusCode.OK,
            content: EMPTY_JSON
        );
        using HttpClient ownerFeedClient = HttpClientTestFactory.Create(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        try
        {
            RepoEventPoller poller = this.CreatePoller();

            await poller.PollAsync(this.CancellationToken());

            Assert.Equal(expected: storedETag, actual: repoHandler.LastRequestIfNoneMatch);
        }
        finally
        {
            repoFeedClient.Dispose();
        }
    }

    [Fact]
    public async Task PollAsync_WhenRepoFeedReturns304_DoesNotProcessEventsOrUpdateLastIdAsync()
    {
        this._eTagStore.GetETagAsync("events.repo.etag:owner/repo", Arg.Any<CancellationToken>())
            .Returns("\"cached-etag\"");

        using HttpClient repoFeedClient = HttpClientTestFactory.Create(HttpStatusCode.NotModified);
        using HttpClient ownerFeedClient = HttpClientTestFactory.Create(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        RepoEventPoller poller = this.CreatePoller();

        await poller.PollAsync(this.CancellationToken());

        Assert.Empty(this._notificationStateTracker.ReceivedCalls());
        await this
            ._eTagStore.DidNotReceive()
            .SaveETagAsync(
                key: "events.repo.lastid:owner/repo",
                eTag: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PollAsync_WhenRepoFeedReturnsNewETag_SavesETagForFeedAsync()
    {
        (HttpClient repoFeedClient, FixedResponseHandler _) = HttpClientTestFactory.CreateWithHandler(
            statusCode: HttpStatusCode.OK,
            content: EMPTY_JSON,
            eTag: "\"new-etag\""
        );
        using HttpClient ownerFeedClient = HttpClientTestFactory.Create(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        try
        {
            RepoEventPoller poller = this.CreatePoller();

            await poller.PollAsync(this.CancellationToken());

            await this
                ._eTagStore.Received(1)
                .SaveETagAsync(
                    key: "events.repo.etag:owner/repo",
                    eTag: "\"new-etag\"",
                    cancellationToken: Arg.Any<CancellationToken>()
                );
        }
        finally
        {
            repoFeedClient.Dispose();
        }
    }

    [Fact]
    public async Task PollAsync_WhenFeedReturnsPollIntervalHeader_ReturnsSuggestedIntervalAsync()
    {
        (HttpClient repoFeedClient, FixedResponseHandler _) = HttpClientTestFactory.CreateWithHandler(
            statusCode: HttpStatusCode.OK,
            content: EMPTY_JSON,
            pollIntervalSeconds: 90
        );
        using HttpClient ownerFeedClient = HttpClientTestFactory.Create(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        try
        {
            RepoEventPoller poller = this.CreatePoller();

            int? result = await poller.PollAsync(this.CancellationToken());

            Assert.Equal(expected: 90, actual: result);
        }
        finally
        {
            repoFeedClient.Dispose();
        }
    }

    [Fact]
    public async Task PollAsync_WhenFeedsReturnDifferentPollIntervals_ReturnsMaxAsync()
    {
        (HttpClient repoFeedClient, FixedResponseHandler _) = HttpClientTestFactory.CreateWithHandler(
            statusCode: HttpStatusCode.OK,
            content: EMPTY_JSON,
            pollIntervalSeconds: 60
        );
        (HttpClient ownerFeedClient, FixedResponseHandler _) = HttpClientTestFactory.CreateWithHandler(
            statusCode: HttpStatusCode.OK,
            content: EMPTY_JSON,
            pollIntervalSeconds: 120
        );
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        try
        {
            RepoEventPoller poller = this.CreatePoller();

            int? result = await poller.PollAsync(this.CancellationToken());

            Assert.Equal(expected: 120, actual: result);
        }
        finally
        {
            repoFeedClient.Dispose();
            ownerFeedClient.Dispose();
        }
    }

    [Fact]
    public async Task PollAsync_WithLateEventOlderThanLastIdButNotSeen_ProcessesItAsync()
    {
        this.GivenStoredRepoFeedState(lastId: "300", seenIds: "300");

        string feed = BuildFeedJson([
            BuildIssueEventJson(id: "300", number: "60"),
            BuildIssueEventJson(id: "250", number: "61"),
        ]);
        using HttpClient repoFeedClient = HttpClientTestFactory.Create(HttpStatusCode.OK, feed);
        using HttpClient ownerFeedClient = HttpClientTestFactory.Create(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options);

        await poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: [61], actual: this.IssueNumbersUpdatedInCallOrder());
        await this
            ._eTagStore.Received(1)
            .SaveETagAsync(key: REPO_SEEN_IDS_KEY, eTag: "300,250", cancellationToken: Arg.Any<CancellationToken>());
        await this
            ._eTagStore.DidNotReceive()
            .SaveETagAsync(
                key: REPO_LAST_ID_KEY,
                eTag: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PollAsync_WithEventsAlreadySeen_DoesNotProcessThemAgainAsync()
    {
        this.GivenStoredRepoFeedState(lastId: "300", seenIds: "300,250");

        string feed = BuildFeedJson([
            BuildIssueEventJson(id: "300", number: "60"),
            BuildIssueEventJson(id: "250", number: "61"),
        ]);
        using HttpClient repoFeedClient = HttpClientTestFactory.Create(HttpStatusCode.OK, feed);
        using HttpClient ownerFeedClient = HttpClientTestFactory.Create(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoFeedClient, ownerFeedClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options);

        await poller.PollAsync(this.CancellationToken());

        Assert.Empty(this.IssueNumbersUpdatedInCallOrder());
        await this
            ._eTagStore.DidNotReceive()
            .SaveETagAsync(
                key: REPO_SEEN_IDS_KEY,
                eTag: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PollAsync_WithDuplicateEventAcrossOverlappingPages_ProcessesItOnceAsync()
    {
        this.GivenStoredRepoFeedState(lastId: "100", seenIds: "100");

        string firstPage = BuildFeedJson([
            BuildIssueEventJson(id: "300", number: "62"),
            BuildIssueEventJson(id: "200", number: "61"),
        ]);
        string secondPage = BuildFeedJson([
            BuildIssueEventJson(id: "200", number: "61"),
            BuildIssueEventJson(id: "100", number: "60"),
        ]);
        using HttpClient firstPageClient = HttpClientTestFactory.Create(
            statusCode: HttpStatusCode.OK,
            content: firstPage,
            linkUrl: "https://api.github.com/repositories/1/events?per_page=30&page=2"
        );
        using HttpClient secondPageClient = HttpClientTestFactory.Create(HttpStatusCode.OK, secondPage);
        using HttpClient ownerFeedClient = HttpClientTestFactory.Create(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(firstPageClient, secondPageClient, ownerFeedClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options);

        await poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: [61, 62], actual: this.IssueNumbersUpdatedInCallOrder());
    }

    [Fact]
    public async Task PollAsync_WithMoreThanOnePageOfNewEvents_FollowsNextLinkAndProcessesAllOldestFirstAsync()
    {
        this.GivenStoredRepoFeedState(lastId: "100", seenIds: "100");

        string firstPage = BuildFeedJson([
            BuildIssueEventJson(id: "400", number: "63"),
            BuildIssueEventJson(id: "300", number: "62"),
        ]);
        string secondPage = BuildFeedJson([
            BuildIssueEventJson(id: "200", number: "61"),
            BuildIssueEventJson(id: "100", number: "60"),
        ]);
        (HttpClient firstPageClient, FixedResponseHandler _) = HttpClientTestFactory.CreateWithHandler(
            statusCode: HttpStatusCode.OK,
            content: firstPage,
            linkUrl: "https://api.github.com/repositories/1/events?per_page=30&page=2"
        );
        (HttpClient secondPageClient, FixedResponseHandler secondPageHandler) = HttpClientTestFactory.CreateWithHandler(
            statusCode: HttpStatusCode.OK,
            content: secondPage
        );
        using HttpClient ownerFeedClient = HttpClientTestFactory.Create(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(firstPageClient, secondPageClient, ownerFeedClient);

        try
        {
            GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
            RepoEventPoller poller = this.CreatePoller(options);

            await poller.PollAsync(this.CancellationToken());

            Assert.Equal(expected: [61, 62, 63], actual: this.IssueNumbersUpdatedInCallOrder());
            Assert.Equal(
                expected: "/repositories/1/events?per_page=30&page=2",
                actual: secondPageHandler.LastRequestUri?.PathAndQuery
            );
            await this
                ._eTagStore.Received(1)
                .SaveETagAsync(key: REPO_LAST_ID_KEY, eTag: "400", cancellationToken: Arg.Any<CancellationToken>());
            await this
                ._eTagStore.Received(1)
                .SaveETagAsync(
                    key: REPO_SEEN_IDS_KEY,
                    eTag: "400,300,200,100",
                    cancellationToken: Arg.Any<CancellationToken>()
                );
        }
        finally
        {
            firstPageClient.Dispose();
            secondPageClient.Dispose();
        }
    }

    [Fact]
    public async Task PollAsync_WithPageReachingLastId_DoesNotFollowNextLinkAsync()
    {
        this.GivenStoredRepoFeedState(lastId: "300", seenIds: "300");

        string firstPage = BuildFeedJson([
            BuildIssueEventJson(id: "400", number: "63"),
            BuildIssueEventJson(id: "300", number: "62"),
        ]);
        using HttpClient firstPageClient = HttpClientTestFactory.Create(
            statusCode: HttpStatusCode.OK,
            content: firstPage,
            linkUrl: "https://api.github.com/repositories/1/events?per_page=30&page=2"
        );
        using HttpClient ownerFeedClient = HttpClientTestFactory.Create(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(firstPageClient, ownerFeedClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options);

        await poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: [63], actual: this.IssueNumbersUpdatedInCallOrder());

        // One request for the repo feed's first page and one for the owner feed: no next-page request.
        Assert.Equal(expected: 2, actual: this._httpClientFactory.ReceivedCalls().Count());
    }

    [Fact]
    public async Task PollAsync_WithNoStoredLastId_ReadsOnlyFirstPageAsync()
    {
        string firstPage = BuildFeedJson([
            BuildIssueEventJson(id: "400", number: "63"),
            BuildIssueEventJson(id: "300", number: "62"),
        ]);
        using HttpClient firstPageClient = HttpClientTestFactory.Create(
            statusCode: HttpStatusCode.OK,
            content: firstPage,
            linkUrl: "https://api.github.com/repositories/1/events?per_page=30&page=2"
        );
        using HttpClient ownerFeedClient = HttpClientTestFactory.Create(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(firstPageClient, ownerFeedClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };
        RepoEventPoller poller = this.CreatePoller(options);

        await poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: [62, 63], actual: this.IssueNumbersUpdatedInCallOrder());

        // One request for the repo feed's first page and one for the owner feed: no next-page request.
        Assert.Equal(expected: 2, actual: this._httpClientFactory.ReceivedCalls().Count());
    }
}
