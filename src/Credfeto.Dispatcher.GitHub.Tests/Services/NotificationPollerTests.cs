using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using Credfeto.Dispatcher.GitHub.Tests.Helpers;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Extensions;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class NotificationPollerTests : TestBase
{
    private const string NOTIFICATION_JSON = """
        [
          {
            "id": "1",
            "reason": "mention",
            "subject": {
              "title": "A pull request",
              "url": "https://api.github.com/repos/owner/repo/pulls/1",
              "type": "PullRequest"
            },
            "repository": {
              "full_name": "owner/repo",
              "html_url": "https://github.com/owner/repo"
            },
            "updated_at": "2024-01-01T00:00:00Z",
            "unread": true
          }
        ]
        """;

    private const string SECOND_PAGE_JSON = """
        [
          {
            "id": "2",
            "reason": "review_requested",
            "subject": {
              "title": "Another pull request",
              "url": "https://api.github.com/repos/owner/repo/pulls/2",
              "type": "PullRequest"
            },
            "repository": {
              "full_name": "owner/repo",
              "html_url": "https://github.com/owner/repo"
            },
            "updated_at": "2024-01-02T00:00:00Z",
            "unread": true
          }
        ]
        """;

    private const string STORED_ETAG = "\"stored-etag\"";
    private const string FIRST_PAGE_ETAG = "\"page-1-etag\"";
    private const string SECOND_PAGE_LINK = "https://api.github.com/notifications?per_page=100&page=2";

    private readonly IETagStore _eTagStore;
    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly INotificationPoller _poller;

    public NotificationPollerTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        this._eTagStore = GetSubstitute<IETagStore>();
        this._poller = new NotificationPoller(
            httpClientFactory: this._httpClientFactory,
            eTagStore: this._eTagStore,
            logger: this.GetTypedLogger<NotificationPoller>()
        );
    }

    [Fact]
    public async Task PollAsyncReturnsNotificationsWhenServerRespondsWithOkAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: NOTIFICATION_JSON
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.NotEmpty(result.Notifications);
    }

    [Fact]
    public async Task PollAsyncReturnsSingleNotificationWhenServerRespondsWithOkAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: NOTIFICATION_JSON
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Single(result.Notifications);
    }

    [Fact]
    public async Task PollAsyncMapsNotificationIdCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: NOTIFICATION_JSON
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: "1", actual: result.Notifications[0].Id);
    }

    [Fact]
    public async Task PollAsyncMapsNotificationReasonCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: NOTIFICATION_JSON
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: "mention", actual: result.Notifications[0].Reason);
    }

    [Fact]
    public async Task PollAsyncMapsSubjectTitleCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: NOTIFICATION_JSON
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: "A pull request", actual: result.Notifications[0].Subject.Title);
    }

    [Fact]
    public async Task PollAsyncMapsRepositoryFullNameCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: NOTIFICATION_JSON
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: "owner/repo", actual: result.Notifications[0].Repository.FullName);
    }

    [Fact]
    public async Task PollAsyncReturnsEmptyListWhenServerRespondsWithNotModifiedAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.NotModified
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Empty(result.Notifications);
    }

    [Fact]
    public async Task PollAsyncReturnsEmptyListOnSecondNotModifiedResponseAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: NOTIFICATION_JSON
        );
        await this._poller.PollAsync(this.CancellationToken());

        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.NotModified
        );
        NotificationPollResult secondResult = await this._poller.PollAsync(this.CancellationToken());

        Assert.Empty(secondResult.Notifications);
    }

    [Fact]
    public async Task PollAsyncReturnsEmptyListWhenServerRespondsWithNullBodyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: "null"
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Empty(result.Notifications);
    }

    [Fact]
    public async Task PollAsyncMapsUnreadFlagCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: NOTIFICATION_JSON
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.True(result.Notifications[0].Unread, userMessage: "Expected unread flag to be true");
    }

    [Fact]
    public async Task PollAsyncMapsSubjectTypeCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: NOTIFICATION_JSON
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: "PullRequest", actual: result.Notifications[0].Subject.Type);
    }

    [Fact]
    public async Task PollAsyncMapsRepositoryUrlCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: NOTIFICATION_JSON
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(
            expected: new Uri("https://github.com/owner/repo"),
            actual: result.Notifications[0].Repository.Url
        );
    }

    [Fact]
    public async Task PollAsyncReturnsNullUrlWhenUrlIsNullAsync()
    {
        const string jsonWithNullUrl = """
            [
              {
                "id": "2",
                "reason": "subscribed",
                "subject": {
                  "title": "A commit",
                  "url": null,
                  "type": "Commit"
                },
                "repository": {
                  "full_name": "owner/repo",
                  "html_url": "https://github.com/owner/repo"
                },
                "updated_at": "2024-01-01T00:00:00Z",
                "unread": false
              }
            ]
            """;

        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: jsonWithNullUrl
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Null(result.Notifications[0].Subject.Url);
    }

    [Fact]
    public async Task PollAsyncLoadsETagFromStoreOnFirstCallAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: NOTIFICATION_JSON
        );

        await this._poller.PollAsync(this.CancellationToken());

        await this
            ._eTagStore.Received(1)
            .GetETagAsync(key: "github.notifications", cancellationToken: this.CancellationToken());
    }

    [Fact]
    public async Task PollAsyncDoesNotSendIfNoneMatchWhenStoredETagIsEmptyTagAsync()
    {
        this._eTagStore.GetETagAsync(key: "github.notifications", cancellationToken: this.CancellationToken())
            .Returns("\"\"");

        using CapturingResponseHandler handler = new(statusCode: HttpStatusCode.OK, content: NOTIFICATION_JSON);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.github.com/") };
        this._httpClientFactory.CreateClient("GitHub").Returns(httpClient);

        await this._poller.PollAsync(this.CancellationToken());

        Assert.NotNull(handler.CapturedRequest);
        Assert.Empty(handler.CapturedRequest.Headers.IfNoneMatch);
    }

    [Fact]
    public async Task PollAsyncReturnsNullCandidateETagWhenResponseHasEmptyTagAsync()
    {
        using FixedResponseHandler handler = new(
            statusCode: HttpStatusCode.OK,
            content: NOTIFICATION_JSON,
            eTag: "\"\""
        );
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.github.com/") };
        this._httpClientFactory.CreateClient("GitHub").Returns(httpClient);

        NotificationPollResult result = await this._poller.PollAsync(TestContext.Current.CancellationToken);

        Assert.Null(result.CandidateETag);
    }

    [Fact]
    public async Task PollAsyncNeverSavesETagItselfAsync()
    {
        using FixedResponseHandler handler = new(
            statusCode: HttpStatusCode.OK,
            content: NOTIFICATION_JSON,
            eTag: "\"new-etag\""
        );
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.github.com/") };
        this._httpClientFactory.CreateClient("GitHub").Returns(httpClient);

        await this._poller.PollAsync(TestContext.Current.CancellationToken);

        await this
            ._eTagStore.DidNotReceive()
            .SaveETagAsync(
                key: Arg.Any<string>(),
                eTag: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PollAsyncReturnsNotificationsWhenStoredETagIsEmptyTagAsync()
    {
        this._eTagStore.GetETagAsync(key: "github.notifications", cancellationToken: this.CancellationToken())
            .Returns("\"\"");

        using CapturingResponseHandler handler = new(statusCode: HttpStatusCode.OK, content: NOTIFICATION_JSON);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.github.com/") };
        this._httpClientFactory.CreateClient("GitHub").Returns(httpClient);

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.NotEmpty(result.Notifications);
    }

    [Fact]
    public async Task PollAsyncReturnsCandidateETagWhenResponseIncludesETagAsync()
    {
        const string eTagJson = """
            [
              {
                "id": "3",
                "reason": "mention",
                "subject": {
                  "title": "A PR",
                  "url": "https://api.github.com/repos/owner/repo/pulls/3",
                  "type": "PullRequest"
                },
                "repository": {
                  "full_name": "owner/repo",
                  "html_url": "https://github.com/owner/repo"
                },
                "updated_at": "2024-01-01T00:00:00Z",
                "unread": true
              }
            ]
            """;

        using FixedResponseHandler handler = new(
            statusCode: HttpStatusCode.OK,
            content: eTagJson,
            eTag: "\"new-etag\""
        );
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.github.com/") };
        this._httpClientFactory.CreateClient("GitHub").Returns(httpClient);

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: "\"new-etag\"", actual: result.CandidateETag);
    }

    [Fact]
    public async Task PollAsyncRequestsFirstPageWithPerPage100Async()
    {
        using SequencedResponseHandler handler = new(SinglePage());
        using HttpClient httpClient = MockHttpClientFactoryCreateClient(
            httpClientFactory: this._httpClientFactory,
            handler: handler
        );

        await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: "/notifications?per_page=100", actual: handler.RequestUris[0]?.PathAndQuery);
    }

    [Fact]
    public async Task PollAsyncMakesOneRequestWhenThereIsNoNextLinkAsync()
    {
        using SequencedResponseHandler handler = new(SinglePage());
        using HttpClient httpClient = MockHttpClientFactoryCreateClient(
            httpClientFactory: this._httpClientFactory,
            handler: handler
        );

        await this._poller.PollAsync(this.CancellationToken());

        Assert.Single(handler.RequestUris);
    }

    [Fact]
    public async Task PollAsyncReturnsNotificationsFromEveryPageInOrderAsync()
    {
        using SequencedResponseHandler handler = new(FirstOfTwoPages(), SecondPage());
        using HttpClient httpClient = MockHttpClientFactoryCreateClient(
            httpClientFactory: this._httpClientFactory,
            handler: handler
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: ["1", "2"], actual: result.Notifications.Select(n => n.Id));
    }

    [Fact]
    public async Task PollAsyncFollowsNextLinkRelativeToBaseAddressAsync()
    {
        using SequencedResponseHandler handler = new(FirstOfTwoPages(), SecondPage());
        using HttpClient httpClient = MockHttpClientFactoryCreateClient(
            httpClientFactory: this._httpClientFactory,
            handler: handler
        );

        await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: new Uri(SECOND_PAGE_LINK), actual: handler.RequestUris[1]);
    }

    [Fact]
    public async Task PollAsyncSendsIfNoneMatchOnFirstPageAsync()
    {
        MockETagStoreGetETag(
            eTagStore: this._eTagStore,
            eTag: STORED_ETAG,
            cancellationToken: this.CancellationToken()
        );
        using SequencedResponseHandler handler = new(FirstOfTwoPages(), SecondPage());
        using HttpClient httpClient = MockHttpClientFactoryCreateClient(
            httpClientFactory: this._httpClientFactory,
            handler: handler
        );

        await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: STORED_ETAG, actual: handler.RequestIfNoneMatches[0]);
    }

    [Fact]
    public async Task PollAsyncDoesNotSendIfNoneMatchOnLaterPagesAsync()
    {
        MockETagStoreGetETag(
            eTagStore: this._eTagStore,
            eTag: STORED_ETAG,
            cancellationToken: this.CancellationToken()
        );
        using SequencedResponseHandler handler = new(FirstOfTwoPages(), SecondPage());
        using HttpClient httpClient = MockHttpClientFactoryCreateClient(
            httpClientFactory: this._httpClientFactory,
            handler: handler
        );

        await this._poller.PollAsync(this.CancellationToken());

        Assert.Null(handler.RequestIfNoneMatches[1]);
    }

    [Fact]
    public async Task PollAsyncReturnsFirstPageETagAsCandidateWhenPaginatedAsync()
    {
        using SequencedResponseHandler handler = new(FirstOfTwoPages(), SecondPage());
        using HttpClient httpClient = MockHttpClientFactoryCreateClient(
            httpClientFactory: this._httpClientFactory,
            handler: handler
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: FIRST_PAGE_ETAG, actual: result.CandidateETag);
    }

    [Fact]
    public async Task PollAsyncMakesNoFurtherRequestsWhenFirstPageIsNotModifiedAsync()
    {
        MockETagStoreGetETag(
            eTagStore: this._eTagStore,
            eTag: STORED_ETAG,
            cancellationToken: this.CancellationToken()
        );
        using SequencedResponseHandler handler = new(NotModifiedPage(), SecondPage());
        using HttpClient httpClient = MockHttpClientFactoryCreateClient(
            httpClientFactory: this._httpClientFactory,
            handler: handler
        );

        await this._poller.PollAsync(this.CancellationToken());

        Assert.Single(handler.RequestUris);
    }

    [Fact]
    public async Task PollAsyncReturnsEmptyResultWhenNotModifiedAsync()
    {
        MockETagStoreGetETag(
            eTagStore: this._eTagStore,
            eTag: STORED_ETAG,
            cancellationToken: this.CancellationToken()
        );
        using SequencedResponseHandler handler = new(NotModifiedPage());
        using HttpClient httpClient = MockHttpClientFactoryCreateClient(
            httpClientFactory: this._httpClientFactory,
            handler: handler
        );

        NotificationPollResult result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Empty(result.Notifications);
        Assert.Null(result.CandidateETag);
    }

    [Fact]
    public async Task PollAsyncThrowsWhenALaterPageFailsAsync()
    {
        using SequencedResponseHandler handler = new(FirstOfTwoPages(), FailedPage());
        using HttpClient httpClient = MockHttpClientFactoryCreateClient(
            httpClientFactory: this._httpClientFactory,
            handler: handler
        );

        await Assert.ThrowsAsync<HttpRequestException>(() => this._poller.PollAsync(this.CancellationToken()).AsTask());
    }

    [Fact]
    public async Task PollAsyncDoesNotSaveETagWhenALaterPageFailsAsync()
    {
        using SequencedResponseHandler handler = new(FirstOfTwoPages(), FailedPage());
        using HttpClient httpClient = MockHttpClientFactoryCreateClient(
            httpClientFactory: this._httpClientFactory,
            handler: handler
        );

        await Assert.ThrowsAsync<HttpRequestException>(() => this._poller.PollAsync(this.CancellationToken()).AsTask());

        await this
            ._eTagStore.DidNotReceive()
            .SaveETagAsync(
                key: Arg.Any<string>(),
                eTag: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task CommitETagAsyncSavesTheCandidateETagToTheStoreAsync()
    {
        await this._poller.CommitETagAsync(candidateETag: "\"new-etag\"", cancellationToken: this.CancellationToken());

        await this
            ._eTagStore.Received(1)
            .SaveETagAsync(
                key: "github.notifications",
                eTag: "\"new-etag\"",
                cancellationToken: this.CancellationToken()
            );
    }

    private static SequencedResponse SinglePage()
    {
        return new(StatusCode: HttpStatusCode.OK, Content: NOTIFICATION_JSON, ETag: FIRST_PAGE_ETAG, LinkUrl: null);
    }

    private static SequencedResponse FirstOfTwoPages()
    {
        return new(
            StatusCode: HttpStatusCode.OK,
            Content: NOTIFICATION_JSON,
            ETag: FIRST_PAGE_ETAG,
            LinkUrl: SECOND_PAGE_LINK
        );
    }

    private static SequencedResponse SecondPage()
    {
        return new(StatusCode: HttpStatusCode.OK, Content: SECOND_PAGE_JSON, ETag: "\"page-2-etag\"", LinkUrl: null);
    }

    private static SequencedResponse FailedPage()
    {
        return new(StatusCode: HttpStatusCode.InternalServerError, Content: null, ETag: null, LinkUrl: null);
    }

    private static SequencedResponse NotModifiedPage()
    {
        return new(StatusCode: HttpStatusCode.NotModified, Content: null, ETag: STORED_ETAG, LinkUrl: null);
    }

    private static void MockETagStoreGetETag(IETagStore eTagStore, string eTag, in CancellationToken cancellationToken)
    {
        eTagStore.GetETagAsync(key: "github.notifications", cancellationToken: cancellationToken).Returns(eTag);
    }

    private static HttpClient MockHttpClientFactoryCreateClient(
        System.Net.Http.IHttpClientFactory httpClientFactory,
        HttpMessageHandler handler
    )
    {
        HttpClient httpClient = new(handler: handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        httpClientFactory.CreateClient("GitHub").Returns(httpClient);

        return httpClient;
    }
}
