using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using FunFair.Test.Common;
using FunFair.Test.Common.Extensions;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class NotificationPollerTests : TestBase
{
    private const string NotificationJson =
        """
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

    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly INotificationPoller _poller;

    public NotificationPollerTests()
    {
        this._httpClientFactory = Substitute.For<System.Net.Http.IHttpClientFactory>();
        this._poller = new NotificationPoller(this._httpClientFactory);
    }

    [Fact]
    public async Task PollAsyncReturnsNotificationsWhenServerRespondsWithOkAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK, responseMessage: NotificationJson);

        IReadOnlyList<GitHubNotification> result = await this._poller.PollAsync(this.CancellationToken());

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task PollAsyncReturnsSingleNotificationWhenServerRespondsWithOkAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK, responseMessage: NotificationJson);

        IReadOnlyList<GitHubNotification> result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Single(result);
    }

    [Fact]
    public async Task PollAsyncMapsNotificationIdCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK, responseMessage: NotificationJson);

        IReadOnlyList<GitHubNotification> result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: "1", actual: result[0].Id);
    }

    [Fact]
    public async Task PollAsyncMapsNotificationReasonCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK, responseMessage: NotificationJson);

        IReadOnlyList<GitHubNotification> result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: "mention", actual: result[0].Reason);
    }

    [Fact]
    public async Task PollAsyncMapsSubjectTitleCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK, responseMessage: NotificationJson);

        IReadOnlyList<GitHubNotification> result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: "A pull request", actual: result[0].Subject.Title);
    }

    [Fact]
    public async Task PollAsyncMapsRepositoryFullNameCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK, responseMessage: NotificationJson);

        IReadOnlyList<GitHubNotification> result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: "owner/repo", actual: result[0].Repository.FullName);
    }

    [Fact]
    public async Task PollAsyncReturnsEmptyListWhenServerRespondsWithNotModifiedAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.NotModified);

        IReadOnlyList<GitHubNotification> result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Empty(result);
    }

    [Fact]
    public async Task PollAsyncReturnsCachedResultOnSecondNotModifiedResponseAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK, responseMessage: NotificationJson);
        IReadOnlyList<GitHubNotification> firstResult = await this._poller.PollAsync(this.CancellationToken());

        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.NotModified);
        IReadOnlyList<GitHubNotification> secondResult = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: firstResult.Count, actual: secondResult.Count);
    }

    [Fact]
    public async Task PollAsyncReturnsEmptyListWhenServerRespondsWithNullBodyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK, responseMessage: "null");

        IReadOnlyList<GitHubNotification> result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Empty(result);
    }

    [Fact]
    public async Task PollAsyncMapsUnreadFlagCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK, responseMessage: NotificationJson);

        IReadOnlyList<GitHubNotification> result = await this._poller.PollAsync(this.CancellationToken());

        Assert.True(result[0].Unread, userMessage: "Expected unread flag to be true");
    }

    [Fact]
    public async Task PollAsyncMapsSubjectTypeCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK, responseMessage: NotificationJson);

        IReadOnlyList<GitHubNotification> result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: "PullRequest", actual: result[0].Subject.Type);
    }

    [Fact]
    public async Task PollAsyncMapsRepositoryUrlCorrectlyAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK, responseMessage: NotificationJson);

        IReadOnlyList<GitHubNotification> result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: new Uri("https://github.com/owner/repo"), actual: result[0].Repository.Url);
    }

    [Fact]
    public async Task PollAsyncUsesSubjectUrlFallbackWhenUrlIsNullAsync()
    {
        const string jsonWithNullUrl =
            """
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

        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK, responseMessage: jsonWithNullUrl);

        IReadOnlyList<GitHubNotification> result = await this._poller.PollAsync(this.CancellationToken());

        Assert.Equal(expected: new Uri("about:blank"), actual: result[0].Subject.Url);
    }
}
