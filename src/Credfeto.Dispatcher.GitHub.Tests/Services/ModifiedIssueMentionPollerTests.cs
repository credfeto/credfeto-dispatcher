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
using FunFair.Test.Infrastructure.Mocks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class ModifiedIssueMentionPollerTests : TestBase
{
    private const string REPO = "owner/repo";

    private const string SINCE_KEY = "issues.mentions.since:owner/repo";

    private const string MENTIONED_USER = "octocat";

    private const string PAGE_2_URL = "https://api.github.com/repos/owner/repo/issues?state=open&per_page=100&page=2";

    private const string PAGE_3_URL = "https://api.github.com/repos/owner/repo/issues?state=open&per_page=100&page=3";

    private const string PAGE_1_WITH_MENTION_JSON = """
        [{
          "number": 10,
          "title": "Mentioned issue",
          "state": "open",
          "html_url": "https://github.com/owner/repo/issues/10",
          "body": "Hey @octocat please look",
          "updated_at": "2024-01-01T00:00:00Z",
          "assignees": [],
          "labels": [],
          "pull_request": null
        }]
        """;

    private const string PAGE_3_WITH_MENTION_JSON = """
        [{
          "number": 30,
          "title": "Another mentioned issue",
          "state": "open",
          "html_url": "https://github.com/owner/repo/issues/30",
          "body": "@octocat ping",
          "updated_at": "2024-01-02T00:00:00Z",
          "assignees": [],
          "labels": [],
          "pull_request": null
        }]
        """;

    private const string MIXED_ITEMS_JSON = """
        [{
          "number": 40,
          "title": "Mentioned pull request",
          "state": "open",
          "html_url": "https://github.com/owner/repo/pull/40",
          "body": "@octocat review please",
          "updated_at": "2024-01-01T00:00:00Z",
          "assignees": [],
          "labels": [],
          "pull_request": { "html_url": "https://github.com/owner/repo/pull/40" }
        },
        {
          "number": 41,
          "title": "Mentioned issue without url",
          "state": "open",
          "html_url": null,
          "body": "@octocat missing url",
          "updated_at": "2024-01-01T00:00:00Z",
          "assignees": [],
          "labels": [],
          "pull_request": null
        },
        {
          "number": 10,
          "title": "Mentioned issue",
          "state": "open",
          "html_url": "https://github.com/owner/repo/issues/10",
          "body": "Hey @octocat please look",
          "updated_at": "2024-01-01T00:00:00Z",
          "assignees": [],
          "labels": [],
          "pull_request": null
        }]
        """;

    private const string EMPTY_JSON = "[]";

    private readonly IActiveRepoTracker _activeRepoTracker;
    private readonly IETagStore _eTagStore;
    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;

    public ModifiedIssueMentionPollerTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        this._activeRepoTracker = GetSubstitute<IActiveRepoTracker>();
        this._eTagStore = GetSubstitute<IETagStore>();

        this._activeRepoTracker.GetActiveReposAsync(Arg.Any<CancellationToken>()).Returns([REPO]);
        this._eTagStore.GetETagAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
    }

    private ModifiedIssueMentionPoller CreatePoller()
    {
        GitHubRepoHelper helper = new(
            httpClientFactory: this._httpClientFactory,
            logger: this.GetTypedLogger<GitHubRepoHelper>()
        );

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { MentionedUser = MENTIONED_USER } };

        return new ModifiedIssueMentionPoller(
            helper: helper,
            activeRepoTracker: this._activeRepoTracker,
            eTagStore: this._eTagStore,
            timeProvider: MockDateTimeSources.Past,
            options: Options.Create(options),
            logger: this.GetTypedLogger<ModifiedIssueMentionPoller>()
        );
    }

    [Fact]
    public async Task PollAsync_WhenAllPagesFetched_SavesWatermarkAsync()
    {
        using HttpClient page1Client = HttpClientTestFactory.Create(
            statusCode: HttpStatusCode.OK,
            content: PAGE_1_WITH_MENTION_JSON,
            linkUrl: PAGE_2_URL
        );
        using HttpClient page2Client = HttpClientTestFactory.Create(statusCode: HttpStatusCode.OK, content: EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(page1Client, page2Client);

        ModifiedIssueMentionPoller poller = this.CreatePoller();

        IReadOnlyList<GitHubNotification> result = await poller.PollAsync(this.CancellationToken());

        GitHubNotification notification = Assert.Single(result);
        Assert.Equal(expected: "modified-mention:owner/repo:issue:10:1704067200", actual: notification.Id);
        await this
            ._eTagStore.Received(1)
            .SaveETagAsync(key: SINCE_KEY, eTag: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollAsync_WhenFirstPageFails_DoesNotSaveWatermarkAsync()
    {
        using HttpClient page1Client = HttpClientTestFactory.Create(statusCode: HttpStatusCode.InternalServerError);
        this._httpClientFactory.CreateClient("GitHub").Returns(page1Client);

        ModifiedIssueMentionPoller poller = this.CreatePoller();

        IReadOnlyList<GitHubNotification> result = await poller.PollAsync(this.CancellationToken());

        Assert.Empty(result);
        await this
            ._eTagStore.DidNotReceive()
            .SaveETagAsync(key: SINCE_KEY, eTag: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollAsync_WhenMiddlePageFails_DoesNotSaveWatermarkButReturnsEarlierMentionsAsync()
    {
        using HttpClient page1Client = HttpClientTestFactory.Create(
            statusCode: HttpStatusCode.OK,
            content: PAGE_1_WITH_MENTION_JSON,
            linkUrl: PAGE_2_URL
        );
        using HttpClient page2Client = HttpClientTestFactory.Create(
            statusCode: HttpStatusCode.Forbidden,
            content: null,
            linkUrl: PAGE_3_URL
        );
        using HttpClient page3Client = HttpClientTestFactory.Create(
            statusCode: HttpStatusCode.OK,
            content: PAGE_3_WITH_MENTION_JSON
        );
        this._httpClientFactory.CreateClient("GitHub").Returns(page1Client, page2Client, page3Client);

        ModifiedIssueMentionPoller poller = this.CreatePoller();

        IReadOnlyList<GitHubNotification> result = await poller.PollAsync(this.CancellationToken());

        GitHubNotification notification = Assert.Single(result);
        Assert.Equal(expected: "modified-mention:owner/repo:issue:10:1704067200", actual: notification.Id);
        await this
            ._eTagStore.DidNotReceive()
            .SaveETagAsync(key: SINCE_KEY, eTag: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollAsync_SkipsPullRequestsAndIssuesWithoutUrlAsync()
    {
        using HttpClient page1Client = HttpClientTestFactory.Create(
            statusCode: HttpStatusCode.OK,
            content: MIXED_ITEMS_JSON
        );
        this._httpClientFactory.CreateClient("GitHub").Returns(page1Client);

        ModifiedIssueMentionPoller poller = this.CreatePoller();

        IReadOnlyList<GitHubNotification> result = await poller.PollAsync(this.CancellationToken());

        GitHubNotification notification = Assert.Single(result);
        Assert.Equal(expected: "modified-mention:owner/repo:issue:10:1704067200", actual: notification.Id);
    }

    [Fact]
    public async Task PollAsync_WithStoredWatermark_QueriesSinceWatermarkAsync()
    {
        const string retainedSince = "2024-01-01T00:00:00.0000000+00:00";
        this._eTagStore.GetETagAsync(SINCE_KEY, Arg.Any<CancellationToken>()).Returns(retainedSince);

        (HttpClient page1Client, FixedResponseHandler handler) = HttpClientTestFactory.CreateWithHandler(
            statusCode: HttpStatusCode.OK,
            content: EMPTY_JSON
        );
        this._httpClientFactory.CreateClient("GitHub").Returns(page1Client);

        try
        {
            ModifiedIssueMentionPoller poller = this.CreatePoller();

            await poller.PollAsync(this.CancellationToken());

            Uri? requestUri = handler.LastRequestUri;
            Assert.NotNull(requestUri);
            Assert.Contains(
                expectedSubstring: "since=2024-01-01T00%3A00%3A00.0000000%2B00%3A00",
                actualString: requestUri.AbsoluteUri,
                comparisonType: StringComparison.Ordinal
            );
        }
        finally
        {
            page1Client.Dispose();
        }
    }
}
