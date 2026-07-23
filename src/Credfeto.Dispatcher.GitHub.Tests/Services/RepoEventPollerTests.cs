using System;
using System.Collections.Generic;
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

        public ValueTask<PullRequestDetails?> FetchAsync(
            GitHubNotification notification,
            CancellationToken cancellationToken
        )
        {
            return ValueTask.FromResult(this._result);
        }
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
            CommitAuthors: commitAuthors
        );
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
                details: Arg.Is<PullRequestDetails>(d => d.Number == 40),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
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
                cancellationToken: Arg.Any<CancellationToken>()
            );
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
}
