using System;
using System.Collections.Generic;
using System.Globalization;
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
using FunFair.Test.Infrastructure.Mocks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class RepoEventPollerTests : TestBase
{
    private const string REPO = "owner/repo";

    private const string EMPTY_JSON = "[]";

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
    private readonly FakeCheckRefresher _checkRefresher;
    private readonly PullRequestCheckTracker _checkTracker;
    private readonly IFeedFreshnessStore _feedFreshnessStore;
    private readonly TimeProvider _timeProvider;
    private readonly IWorkItemRefresher _workItemRefresher;

    public RepoEventPollerTests()
    {
        this._workItemRefresher = GetSubstitute<IWorkItemRefresher>();
        this._checkRefresher = new FakeCheckRefresher();
        this._feedFreshnessStore = GetSubstitute<IFeedFreshnessStore>();
        this._timeProvider = MockDateTimeSources.Past;
        this._checkTracker = new PullRequestCheckTracker(this._timeProvider);
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        this._activeRepoTracker = GetSubstitute<IActiveRepoTracker>();
        this._eTagStore = GetSubstitute<IETagStore>();
        this._notificationStateTracker = GetSubstitute<INotificationStateTracker>();

        this._activeRepoTracker.GetActiveReposAsync(Arg.Any<CancellationToken>()).Returns([REPO]);
        this._eTagStore.GetETagAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
    }

    private RepoEventPoller CreatePoller(GitHubOptions? options = null)
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
            workItemRefresher: this._workItemRefresher,
            checkRefresher: this._checkRefresher,
            checkTracker: this._checkTracker,
            feedFreshnessStore: this._feedFreshnessStore,
            options: Options.Create(options ?? new GitHubOptions()),
            timeProvider: this._timeProvider,
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

    private sealed class FakeCheckRefresher : IPullRequestCheckRefresher
    {
        public int CallCount { get; private set; }

        public int? PollIntervalSeconds { get; set; }

        public ValueTask<int?> RefreshAsync(CancellationToken cancellationToken)
        {
            this.CallCount++;

            return ValueTask.FromResult(this.PollIntervalSeconds);
        }
    }

    private HttpClient UseSequencedClient(SequencedResponseHandler handler)
    {
        HttpClient httpClient = new(handler: handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        this._httpClientFactory.CreateClient("GitHub").Returns(httpClient);

        return httpClient;
    }

    private static SequencedResponse Ok(string content)
    {
        return new SequencedResponse(StatusCode: HttpStatusCode.OK, Content: content, ETag: null, LinkUrl: null);
    }

    private static SequencedResponse Status(HttpStatusCode statusCode)
    {
        return new SequencedResponse(StatusCode: statusCode, Content: null, ETag: null, LinkUrl: null);
    }

    private static string BuildEventJson(string id, string type, string payload, string repo = REPO)
    {
        return $$"""
            {
              "id": "{{id}}",
              "type": "{{type}}",
              "repo": {"name": "{{repo}}"},
              "payload": {{payload}},
              "created_at": "2024-01-01T00:00:00Z"
            }
            """;
    }

    private static string ReviewEventJson(string id, int number)
    {
        return BuildEventJson(
            id: id,
            type: "PullRequestReviewEvent",
            payload: $$$"""{"action": "created", "pull_request": {"number": {{{number}}}}}"""
        );
    }

    private static string IssueCommentEventJson(
        string id,
        int number,
        string login,
        string userType = "User",
        string repo = REPO,
        string createdAt = "2024-01-01T00:00:00Z"
    )
    {
        return $$"""
            {
              "id": "{{id}}",
              "type": "IssueCommentEvent",
              "actor": {"login": "{{login}}"},
              "repo": {"name": "{{repo}}"},
              "payload": {
                "action": "created",
                "issue": {"number": {{number}}, "title": "Issue", "state": "open"},
                "comment": {
                  "body": "A comment",
                  "user": {"login": "{{login}}", "type": "{{userType}}"},
                  "html_url": "https://github.com/{{repo}}/issues/{{number}}#issuecomment-1"
                }
              },
              "created_at": "{{createdAt}}"
            }
            """;
    }

    private List<GitHubNotification> RefreshedIssueNotifications()
    {
        List<GitHubNotification> refreshed = [];

        foreach (object?[] args in this._workItemRefresher.ReceivedCalls().Select(static call => call.GetArguments()))
        {
            if (args is [GitHubNotification { Subject.Type: "Issue" } notification, _])
            {
                refreshed.Add(notification);
            }
        }

        return refreshed;
    }

    private List<string?> RefreshedIssues()
    {
        return [.. this.RefreshedIssueNotifications().Select(static n => n.Subject.Url?.AbsolutePath)];
    }

    private Task PollWithRepoFeedAsync(IReadOnlyList<string> repoFeedEvents, GitHubOptions? options = null)
    {
        return this.PollWithRepoFeedAsync(poller: this.CreatePoller(options), repoFeedEvents: repoFeedEvents);
    }

    // Polls with the same poller each time, so whatever one poll carries over reaches the next.
    private async Task PollWithRepoFeedAsync(RepoEventPoller poller, IReadOnlyList<string> repoFeedEvents)
    {
        using SequencedResponseHandler handler = new(Ok(BuildFeedJson(repoFeedEvents)), Ok(EMPTY_JSON));
        using HttpClient client = this.UseSequencedClient(handler);

        await poller.PollAsync(this.CancellationToken());
    }

    private List<DateTimeOffset> RecordedSyncs()
    {
        List<DateTimeOffset> recorded = [];

        foreach (object?[] args in this._feedFreshnessStore.ReceivedCalls().Select(static call => call.GetArguments()))
        {
            if (args is [DateTimeOffset syncedAt, _])
            {
                recorded.Add(syncedAt);
            }
        }

        return recorded;
    }

    private static string FormatEventTime(in DateTimeOffset time)
    {
        return time.ToUniversalTime().ToString(format: "O", formatProvider: CultureInfo.InvariantCulture);
    }

    private static GitHubOptions WithEventRefreshBudget(int maxEventRefreshesPerPoll)
    {
        return new() { Filter = new GitHubFilterOptions { MaxEventRefreshesPerPoll = maxEventRefreshesPerPoll } };
    }

    private List<(string Repo, int Number)> RefreshedPullRequests()
    {
        List<(string Repo, int Number)> refreshed = [];

        foreach (object?[] args in this._workItemRefresher.ReceivedCalls().Select(static call => call.GetArguments()))
        {
            if (args is [string repo, int number, _])
            {
                refreshed.Add((repo, number));
            }
        }

        return refreshed;
    }

    [Fact]
    public async Task PollAsync_WithReviewEvent_RefreshesThatPullRequestAsync()
    {
        await this.PollWithRepoFeedAsync([ReviewEventJson(id: "300", number: 40)]);

        Assert.Equal(expected: [(REPO, 40)], actual: this.RefreshedPullRequests());
    }

    [Fact]
    public async Task PollAsync_WithReviewCommentEventCarryingOnlyTheNumber_RefreshesThatPullRequestAsync()
    {
        await this.PollWithRepoFeedAsync([
            BuildEventJson(
                id: "301",
                type: "PullRequestReviewCommentEvent",
                payload: """{"action": "created", "number": 41}"""
            ),
        ]);

        Assert.Equal(expected: [(REPO, 41)], actual: this.RefreshedPullRequests());
    }

    [Fact]
    public async Task PollAsync_WithCommentOnPullRequest_RefreshesThatPullRequestAsync()
    {
        await this.PollWithRepoFeedAsync([
            BuildEventJson(
                id: "302",
                type: "IssueCommentEvent",
                payload: """
                {"action": "created", "issue": {"number": 42, "title": "PR", "state": "open",
                  "pull_request": {"html_url": "https://github.com/owner/repo/pull/42"}}}
                """
            ),
        ]);

        Assert.Equal(expected: [(REPO, 42)], actual: this.RefreshedPullRequests());
    }

    [Fact]
    public async Task PollAsync_WithHumanCommentOnPlainIssue_RefreshesThatIssueAsync()
    {
        await this.PollWithRepoFeedAsync([IssueCommentEventJson(id: "303", number: 50, login: "someone")]);

        Assert.Equal(expected: ["/repos/owner/repo/issues/50"], actual: this.RefreshedIssues());
        Assert.Empty(this.RefreshedPullRequests());
    }

    [Theory]
    [InlineData("github-actions[bot]", "Bot")]
    [InlineData("renovate[bot]", "User")]
    [InlineData("helper-app", "Bot")]
    [InlineData("status-poster", "User")]
    [InlineData("Status-Poster", "User")]
    [InlineData("other-automation", "User")]
    public async Task PollAsync_WithBotOrIgnoredCommentOnPlainIssue_RefreshesNothingAsync(string login, string userType)
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { IgnoredCommenters = ["status-poster", "OTHER-AUTOMATION"] },
        };

        await this.PollWithRepoFeedAsync(
            [IssueCommentEventJson(id: "304", number: 50, login: login, userType: userType)],
            options: options
        );

        Assert.Empty(this._workItemRefresher.ReceivedCalls());
    }

    [Fact]
    public async Task PollAsync_WithCommentFromTheMentionedUser_StillRefreshesTheIssueAsync()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { MentionedUser = "owner-login", IgnoredCommenters = ["status-poster"] },
        };

        await this.PollWithRepoFeedAsync(
            [IssueCommentEventJson(id: "312", number: 50, login: "owner-login")],
            options: options
        );

        Assert.Equal(expected: ["/repos/owner/repo/issues/50"], actual: this.RefreshedIssues());
    }

    [Fact]
    public async Task PollAsync_WithNoIgnoredCommenters_RefreshesOnAnyHumanCommentAsync()
    {
        await this.PollWithRepoFeedAsync([IssueCommentEventJson(id: "313", number: 50, login: "status-poster")]);

        Assert.Equal(expected: ["/repos/owner/repo/issues/50"], actual: this.RefreshedIssues());
    }

    [Fact]
    public async Task PollAsync_WithSeveralCommentsOnOneIssue_RefreshesItOnceStampedWithTheNewestAsync()
    {
        await this.PollWithRepoFeedAsync([
            IssueCommentEventJson(id: "306", number: 50, login: "someone", createdAt: "2024-01-01T00:02:00Z"),
            IssueCommentEventJson(id: "305", number: 50, login: "other", createdAt: "2024-01-01T00:01:00Z"),
        ]);

        GitHubNotification refreshed = Assert.Single(this.RefreshedIssueNotifications());
        Assert.Equal(
            expected: new DateTimeOffset(
                year: 2024,
                month: 1,
                day: 1,
                hour: 0,
                minute: 2,
                second: 0,
                offset: TimeSpan.Zero
            ),
            actual: refreshed.UpdatedAt
        );
    }

    [Fact]
    public async Task PollAsync_WithIssueCommentPastTheBudget_ReReadsItOnTheNextPollAsync()
    {
        RepoEventPoller poller = this.CreatePoller(WithEventRefreshBudget(2));

        await this.PollWithRepoFeedAsync(
            poller: poller,
            repoFeedEvents:
            [
                IssueCommentEventJson(id: "309", number: 52, login: "someone"),
                IssueCommentEventJson(id: "308", number: 51, login: "someone"),
                ReviewEventJson(id: "307", number: 40),
            ]
        );

        Assert.Equal(expected: [(REPO, 40)], actual: this.RefreshedPullRequests());
        Assert.Equal(expected: ["/repos/owner/repo/issues/51"], actual: this.RefreshedIssues());

        await this.PollWithRepoFeedAsync(poller: poller, repoFeedEvents: []);

        Assert.Equal(
            expected: ["/repos/owner/repo/issues/51", "/repos/owner/repo/issues/52"],
            actual: this.RefreshedIssues()
        );
    }

    [Fact]
    public async Task PollAsync_WithCommentOnPlainIssueInUntrackedRepo_RefreshesNothingAsync()
    {
        await this.PollWithRepoFeedAsync([
            IssueCommentEventJson(id: "310", number: 50, login: "someone", repo: "owner/untracked"),
        ]);

        Assert.Empty(this._workItemRefresher.ReceivedCalls());
    }

    [Fact]
    public async Task PollAsync_WithSeveralEventsForOnePullRequest_RefreshesItOnceAsync()
    {
        await this.PollWithRepoFeedAsync([
            ReviewEventJson(id: "311", number: 40),
            ReviewEventJson(id: "310", number: 40),
            BuildEventJson(
                id: "309",
                type: "PullRequestReviewCommentEvent",
                payload: """{"action": "created", "pull_request": {"number": 40}}"""
            ),
        ]);

        Assert.Equal(expected: [(REPO, 40)], actual: this.RefreshedPullRequests());
    }

    [Fact]
    public async Task PollAsync_WithReviewInUntrackedRepo_RefreshesNothingAsync()
    {
        using SequencedResponseHandler handler = new(
            Ok(EMPTY_JSON),
            Ok(
                BuildFeedJson([
                    BuildEventJson(
                        id: "320",
                        type: "PullRequestReviewEvent",
                        payload: """{"action": "created", "pull_request": {"number": 5}}""",
                        repo: "owner/untracked"
                    ),
                ])
            )
        );
        using HttpClient client = this.UseSequencedClient(handler);

        await this.CreatePoller().PollAsync(this.CancellationToken());

        Assert.Empty(this._workItemRefresher.ReceivedCalls());
    }

    [Fact]
    public async Task PollAsync_WithPushToBranch_LooksUpItsOpenPullRequestAndRefreshesItAsync()
    {
        using SequencedResponseHandler handler = new(
            Ok(
                BuildFeedJson([
                    BuildEventJson(
                        id: "330",
                        type: "PushEvent",
                        payload: """{"ref": "refs/heads/feature/x", "head": "def456", "before": "abc123"}"""
                    ),
                ])
            ),
            Ok(EMPTY_JSON),
            Ok("""[{"number": 43, "title": "PR", "state": "open", "head": {"sha": "def456", "ref": "feature/x"}}]""")
        );
        using HttpClient client = this.UseSequencedClient(handler);

        await this.CreatePoller().PollAsync(this.CancellationToken());

        Assert.Equal(
            expected: "/repos/owner/repo/pulls?state=open&head=owner:feature%2Fx&per_page=10",
            actual: handler.RequestUris[2]?.PathAndQuery
        );
        Assert.Equal(expected: [(REPO, 43)], actual: this.RefreshedPullRequests());
    }

    [Fact]
    public async Task PollAsync_WithPushOfATag_LooksNothingUpAsync()
    {
        await this.PollWithRepoFeedAsync([
            BuildEventJson(id: "331", type: "PushEvent", payload: """{"ref": "refs/tags/v1.0.0", "head": "def456"}"""),
        ]);

        Assert.Empty(this._workItemRefresher.ReceivedCalls());
    }

    [Fact]
    public async Task PollAsync_WithMoreRefreshesThanTheBudget_CarriesTheRestOverToTheNextPollAsync()
    {
        RepoEventPoller poller = this.CreatePoller(WithEventRefreshBudget(1));

        await this.PollWithRepoFeedAsync(
            poller: poller,
            repoFeedEvents: [ReviewEventJson(id: "341", number: 41), ReviewEventJson(id: "340", number: 40)]
        );

        Assert.Equal(expected: [(REPO, 40)], actual: this.RefreshedPullRequests());
        Assert.Empty(this._checkTracker.SelectCandidates(5));

        await this.PollWithRepoFeedAsync(poller: poller, repoFeedEvents: []);

        Assert.Equal(expected: [(REPO, 40), (REPO, 41)], actual: this.RefreshedPullRequests());
    }

    [Fact]
    public async Task PollAsync_WithIssuesPastTheBudget_ReReadsTheCarriedOverOnesAheadOfNewWorkAsync()
    {
        RepoEventPoller poller = this.CreatePoller(WithEventRefreshBudget(1));

        await this.PollWithRepoFeedAsync(
            poller: poller,
            repoFeedEvents:
            [
                IssueCommentEventJson(id: "601", number: 52, login: "someone"),
                IssueCommentEventJson(id: "600", number: 51, login: "someone"),
            ]
        );
        await this.PollWithRepoFeedAsync(
            poller: poller,
            repoFeedEvents: [IssueCommentEventJson(id: "602", number: 53, login: "someone")]
        );

        Assert.Equal(
            expected: ["/repos/owner/repo/issues/51", "/repos/owner/repo/issues/52"],
            actual: this.RefreshedIssues()
        );

        await this.PollWithRepoFeedAsync(poller: poller, repoFeedEvents: []);
        await this.PollWithRepoFeedAsync(poller: poller, repoFeedEvents: []);

        Assert.Equal(
            expected: ["/repos/owner/repo/issues/51", "/repos/owner/repo/issues/52", "/repos/owner/repo/issues/53"],
            actual: this.RefreshedIssues()
        );
    }

    [Fact]
    public async Task PollAsync_WhilePollsDefer_RecordsTheSyncAsOfTheOldestPendingEventUntilTheCarryOverClearsAsync()
    {
        DateTimeOffset cycleStart = this._timeProvider.GetUtcNow();
        DateTimeOffset oldest = cycleStart.AddMinutes(-15);
        DateTimeOffset older = cycleStart.AddMinutes(-10);
        DateTimeOffset newer = cycleStart.AddMinutes(-5);
        RepoEventPoller poller = this.CreatePoller(WithEventRefreshBudget(1));

        await this.PollWithRepoFeedAsync(
            poller: poller,
            repoFeedEvents:
            [
                IssueCommentEventJson(id: "612", number: 62, login: "someone", createdAt: FormatEventTime(newer)),
                IssueCommentEventJson(id: "611", number: 61, login: "someone", createdAt: FormatEventTime(older)),
                IssueCommentEventJson(id: "610", number: 60, login: "someone", createdAt: FormatEventTime(oldest)),
            ]
        );
        await this.PollWithRepoFeedAsync(poller: poller, repoFeedEvents: []);
        await this.PollWithRepoFeedAsync(poller: poller, repoFeedEvents: []);

        Assert.Equal(expected: [older, newer, cycleStart], actual: this.RecordedSyncs());
        Assert.Equal(expected: 3, actual: this.RefreshedIssues().Count);
    }

    [Fact]
    public async Task PollAsync_WithAPendingEventNewerThanTheStartOfThePoll_RecordsTheStartOfThePollAsync()
    {
        DateTimeOffset cycleStart = this._timeProvider.GetUtcNow();

        await this.PollWithRepoFeedAsync(
            repoFeedEvents:
            [
                IssueCommentEventJson(
                    id: "621",
                    number: 71,
                    login: "someone",
                    createdAt: FormatEventTime(cycleStart.AddSeconds(30))
                ),
                IssueCommentEventJson(
                    id: "620",
                    number: 70,
                    login: "someone",
                    createdAt: FormatEventTime(cycleStart.AddMinutes(-1))
                ),
            ],
            options: WithEventRefreshBudget(1)
        );

        Assert.Equal(expected: [cycleStart], actual: this.RecordedSyncs());
    }

    [Fact]
    public async Task PollAsync_WithTheBudgetTurnedOff_HandsPullRequestsToTheCheckRefreshAndCarriesNothingOverAsync()
    {
        RepoEventPoller poller = this.CreatePoller(WithEventRefreshBudget(0));

        await this.PollWithRepoFeedAsync(
            poller: poller,
            repoFeedEvents:
            [
                IssueCommentEventJson(id: "631", number: 51, login: "someone"),
                ReviewEventJson(id: "630", number: 40),
            ]
        );

        PullRequestCheckState deferred = Assert.Single(this._checkTracker.SelectCandidates(5));
        Assert.Equal(expected: 40, actual: deferred.Number);
        Assert.Empty(this.RecordedSyncs());

        await this.PollWithRepoFeedAsync(poller: poller, repoFeedEvents: []);

        Assert.Empty(this._workItemRefresher.ReceivedCalls());
        Assert.Equal(expected: [this._timeProvider.GetUtcNow()], actual: this.RecordedSyncs());
    }

    [Fact]
    public async Task PollAsync_WhenEveryFeedAnswers_RecordsTheSyncAsOfTheStartOfTheCycleAsync()
    {
        await this.PollWithRepoFeedAsync([ReviewEventJson(id: "350", number: 40)]);

        await this
            ._feedFreshnessStore.Received(1)
            .RecordSyncAsync(
                syncedAt: MockDateTimeSources.Past.GetUtcNow(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PollAsync_WhenEveryFeedIsNotModified_RecordsTheSyncAsync()
    {
        using SequencedResponseHandler handler = new(
            Status(HttpStatusCode.NotModified),
            Status(HttpStatusCode.NotModified)
        );
        using HttpClient client = this.UseSequencedClient(handler);

        await this.CreatePoller().PollAsync(this.CancellationToken());

        await this
            ._feedFreshnessStore.Received(1)
            .RecordSyncAsync(syncedAt: Arg.Any<DateTimeOffset>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollAsync_WhenAFeedFails_DoesNotRecordTheSyncAsync()
    {
        using SequencedResponseHandler handler = new(Ok(EMPTY_JSON), Status(HttpStatusCode.InternalServerError));
        using HttpClient client = this.UseSequencedClient(handler);

        await this.CreatePoller().PollAsync(this.CancellationToken());

        await this
            ._feedFreshnessStore.DidNotReceive()
            .RecordSyncAsync(syncedAt: Arg.Any<DateTimeOffset>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollAsync_RunsTheCheckRefreshAndHonoursItsPollIntervalAsync()
    {
        this._checkRefresher.PollIntervalSeconds = 90;

        using SequencedResponseHandler handler = new(Ok(EMPTY_JSON), Ok(EMPTY_JSON));
        using HttpClient client = this.UseSequencedClient(handler);

        int? interval = await this.CreatePoller().PollAsync(this.CancellationToken());

        Assert.Equal(expected: 1, actual: this._checkRefresher.CallCount);
        Assert.Equal(expected: 90, actual: interval);
    }

    private static string TrimmedPullRequestEventJson(string id, int number, string action, string repo = REPO)
    {
        // The shape the Events API sends now: no html_url, title, state or labels.
        return BuildEventJson(
            id: id,
            type: "PullRequestEvent",
            payload: $$$"""
            {"action": "{{{action}}}", "number": {{{number}}},
              "pull_request": {"id": 1{{{number}}}, "number": {{{number}}},
                "url": "https://api.github.com/repos/{{{repo}}}/pulls/{{{number}}}",
                "base": {"ref": "main", "sha": "aaa111"}, "head": {"ref": "feature/x", "sha": "bbb222"}}
            }
            """,
            repo: repo
        );
    }

    [Fact]
    public async Task PollAsync_WithTrimmedPrEventPayload_RefreshesThePullRequestByNumberAsync()
    {
        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };

        await this.PollWithRepoFeedAsync(
            [TrimmedPullRequestEventJson(id: "400", number: 40, action: "opened")],
            options: options
        );

        Assert.Equal(expected: [(REPO, 40)], actual: this.RefreshedPullRequests());
        Assert.Empty(this._notificationStateTracker.ReceivedCalls());
    }

    [Theory]
    [InlineData("labeled")]
    [InlineData("unlabeled")]
    [InlineData("assigned")]
    [InlineData("closed")]
    [InlineData("reopened")]
    [InlineData("synchronize")]
    public async Task PollAsync_WithPrEventOfAnyAction_RefreshesThePullRequestAsync(string action)
    {
        await this.PollWithRepoFeedAsync([TrimmedPullRequestEventJson(id: "401", number: 41, action: action)]);

        Assert.Equal(expected: [(REPO, 41)], actual: this.RefreshedPullRequests());
    }

    [Fact]
    public async Task PollAsync_WithPrEventCarryingOnlyTheUrl_TakesTheNumberFromTheUrlAsync()
    {
        await this.PollWithRepoFeedAsync([
            BuildEventJson(
                id: "402",
                type: "PullRequestEvent",
                payload: """
                {"action": "closed", "pull_request": {"url": "https://api.github.com/repos/owner/repo/pulls/44"}}
                """
            ),
        ]);

        Assert.Equal(expected: [(REPO, 44)], actual: this.RefreshedPullRequests());
    }

    [Fact]
    public async Task PollAsync_WithPrEventInUntrackedRepo_RefreshesNothingAsync()
    {
        await this.PollWithRepoFeedAsync([
            TrimmedPullRequestEventJson(id: "403", number: 40, action: "labeled", repo: "owner/untracked"),
        ]);

        Assert.Empty(this._workItemRefresher.ReceivedCalls());
    }

    [Fact]
    public async Task PollAsync_WithSeveralPrEventsForOnePullRequest_RefreshesItOnceAsync()
    {
        await this.PollWithRepoFeedAsync([
            TrimmedPullRequestEventJson(id: "406", number: 40, action: "closed"),
            ReviewEventJson(id: "405", number: 40),
            TrimmedPullRequestEventJson(id: "404", number: 40, action: "labeled"),
        ]);

        Assert.Equal(expected: [(REPO, 40)], actual: this.RefreshedPullRequests());
    }

    [Fact]
    public async Task PollAsync_WithMorePrEventsThanTheBudget_ReReadsTheRestOnTheNextPollAsync()
    {
        RepoEventPoller poller = this.CreatePoller(WithEventRefreshBudget(2));

        await this.PollWithRepoFeedAsync(
            poller: poller,
            repoFeedEvents:
            [
                TrimmedPullRequestEventJson(id: "409", number: 42, action: "labeled"),
                TrimmedPullRequestEventJson(id: "408", number: 41, action: "unlabeled"),
                TrimmedPullRequestEventJson(id: "407", number: 40, action: "opened"),
            ]
        );

        Assert.Equal(expected: [(REPO, 40), (REPO, 41)], actual: this.RefreshedPullRequests());

        await this.PollWithRepoFeedAsync(poller: poller, repoFeedEvents: []);

        Assert.Equal(expected: [(REPO, 40), (REPO, 41), (REPO, 42)], actual: this.RefreshedPullRequests());
    }

    [Fact]
    public async Task PollAsync_WithIssueEventMissingFields_RefreshesTheIssueByNumberAsync()
    {
        await this.PollWithRepoFeedAsync([
            BuildEventJson(
                id: "410",
                type: "IssuesEvent",
                payload: """{"action": "labeled", "issue": {"number": 53, "state": "open"}}"""
            ),
        ]);

        Assert.Equal(expected: ["/repos/owner/repo/issues/53"], actual: this.RefreshedIssues());
        Assert.Empty(this._notificationStateTracker.ReceivedCalls());
    }

    [Fact]
    public async Task PollAsync_WithIssueEventCarryingOnlyTheUrl_TakesTheNumberFromTheUrlAsync()
    {
        await this.PollWithRepoFeedAsync([
            BuildEventJson(
                id: "411",
                type: "IssuesEvent",
                payload: """
                {"action": "closed", "issue": {"url": "https://api.github.com/repos/owner/repo/issues/54"}}
                """
            ),
        ]);

        Assert.Equal(expected: ["/repos/owner/repo/issues/54"], actual: this.RefreshedIssues());
    }

    [Theory]
    [InlineData("https://api.github.com/repos/owner/repo/pulls/45", 45)]
    [InlineData("https://api.github.com/repos/owner/repo/issues/46/", 46)]
    [InlineData("https://api.github.com/repos/owner/repo/pulls/abc", 0)]
    [InlineData("https://api.github.com/repos/owner/repo/pulls/-3", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public static void ParseTrailingNumber_TakesOnlyAPositiveNumberAtTheEnd(string? url, int expected)
    {
        Assert.Equal(expected: expected, actual: RepoEventPoller.ParseTrailingNumber(url));
    }

    private static string CreateEventJson(string id, string refName, string refType = "branch", string repo = REPO)
    {
        return BuildEventJson(
            id: id,
            type: "CreateEvent",
            payload: $$"""
            {"ref": "{{refName}}", "ref_type": "{{refType}}", "master_branch": "main", "pusher_type": "user"}
            """,
            repo: repo
        );
    }

    [Theory]
    [InlineData("fix/292-feed-continuous-refresh", "/repos/owner/repo/issues/292")]
    [InlineData("feat/6-thing", "/repos/owner/repo/issues/6")]
    [InlineData("292-thing", "/repos/owner/repo/issues/292")]
    [InlineData("issue-292-thing", "/repos/owner/repo/issues/292")]
    public async Task PollAsync_WithNewBranchNamingAnIssue_RefreshesThatIssueOnlyAsync(string branch, string issuePath)
    {
        await this.PollWithRepoFeedAsync([CreateEventJson(id: "500", refName: branch)]);

        Assert.Equal(expected: [issuePath], actual: this.RefreshedIssues());
        Assert.Empty(this.RefreshedPullRequests());
        Assert.Empty(this._notificationStateTracker.ReceivedCalls());
    }

    [Theory]
    [InlineData("v1.2.3", "tag")]
    [InlineData("292-thing", "tag")]
    [InlineData("feature/add-thing", "branch")]
    [InlineData("main", "branch")]
    [InlineData("main", "repository")]
    public async Task PollAsync_WithNewTagOrUnnumberedBranch_RefreshesNothingAsync(string refName, string refType)
    {
        await this.PollWithRepoFeedAsync([CreateEventJson(id: "501", refName: refName, refType: refType)]);

        Assert.Empty(this._workItemRefresher.ReceivedCalls());
    }

    [Fact]
    public async Task PollAsync_WithNewBranchInUntrackedRepo_RefreshesNothingAsync()
    {
        await this.PollWithRepoFeedAsync([
            CreateEventJson(id: "502", refName: "fix/292-thing", repo: "owner/untracked"),
        ]);

        Assert.Empty(this._workItemRefresher.ReceivedCalls());
    }

    [Theory]
    [InlineData("depends/update-sdk", "depends%2Fupdate-sdk")]
    [InlineData("dependabot/nuget/src/Foo-1.2.3", "dependabot%2Fnuget%2Fsrc%2FFoo-1.2.3")]
    public async Task PollAsync_WithNewDependencyBranch_LooksUpItsOpenPullRequestAndRefreshesItAsync(
        string branch,
        string escapedBranch
    )
    {
        using SequencedResponseHandler handler = new(
            Ok(BuildFeedJson([CreateEventJson(id: "503", refName: branch)])),
            Ok(EMPTY_JSON),
            Ok("""[{"number": 77, "title": "Bump", "state": "open", "head": {"sha": "abc", "ref": "x"}}]""")
        );
        using HttpClient client = this.UseSequencedClient(handler);

        await this.CreatePoller().PollAsync(this.CancellationToken());

        Assert.Equal(
            expected: "/repos/owner/repo/pulls?state=open&head=owner:" + escapedBranch + "&per_page=10",
            actual: handler.RequestUris[2]?.PathAndQuery
        );
        Assert.Equal(expected: [(REPO, 77)], actual: this.RefreshedPullRequests());
        Assert.Empty(this.RefreshedIssues());
    }

    [Fact]
    public async Task PollAsync_WithNewDependencyBranchWithoutAPullRequestYet_RefreshesNothingAsync()
    {
        using SequencedResponseHandler handler = new(
            Ok(BuildFeedJson([CreateEventJson(id: "504", refName: "dependabot/npm/left-pad-2.0.0")])),
            Ok(EMPTY_JSON),
            Ok(EMPTY_JSON)
        );
        using HttpClient client = this.UseSequencedClient(handler);

        await this.CreatePoller().PollAsync(this.CancellationToken());

        Assert.Equal(expected: 3, actual: handler.RequestUris.Count);
        Assert.Empty(this._workItemRefresher.ReceivedCalls());
    }

    [Fact]
    public async Task PollAsync_WithNewDependencyBranchAlsoPushed_LooksItUpOnceAsync()
    {
        using SequencedResponseHandler handler = new(
            Ok(
                BuildFeedJson([
                    BuildEventJson(
                        id: "506",
                        type: "PushEvent",
                        payload: """{"ref": "refs/heads/depends/update-sdk", "head": "def456"}"""
                    ),
                    CreateEventJson(id: "505", refName: "depends/update-sdk"),
                ])
            ),
            Ok(EMPTY_JSON),
            Ok(EMPTY_JSON)
        );
        using HttpClient client = this.UseSequencedClient(handler);

        await this.CreatePoller().PollAsync(this.CancellationToken());

        Assert.Equal(expected: 3, actual: handler.RequestUris.Count);
    }

    [Fact]
    public async Task PollAsync_WithNewBranchAndCommentForOneIssue_RefreshesItOnceAsync()
    {
        await this.PollWithRepoFeedAsync([
            IssueCommentEventJson(id: "508", number: 292, login: "someone"),
            CreateEventJson(id: "507", refName: "fix/292-thing"),
        ]);

        Assert.Equal(expected: ["/repos/owner/repo/issues/292"], actual: this.RefreshedIssues());
    }

    [Fact]
    public async Task PollAsync_WithMoreNewBranchesThanTheBudget_ReReadsTheRestOnTheNextPollAsync()
    {
        RepoEventPoller poller = this.CreatePoller(WithEventRefreshBudget(1));

        await this.PollWithRepoFeedAsync(
            poller: poller,
            repoFeedEvents:
            [
                CreateEventJson(id: "510", refName: "fix/7-b"),
                CreateEventJson(id: "509", refName: "fix/6-a"),
            ]
        );

        Assert.Equal(expected: ["/repos/owner/repo/issues/6"], actual: this.RefreshedIssues());

        await this.PollWithRepoFeedAsync(poller: poller, repoFeedEvents: []);

        Assert.Equal(
            expected: ["/repos/owner/repo/issues/6", "/repos/owner/repo/issues/7"],
            actual: this.RefreshedIssues()
        );
    }
}
