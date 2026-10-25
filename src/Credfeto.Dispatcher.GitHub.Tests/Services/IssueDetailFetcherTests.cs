using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using Credfeto.Dispatcher.GitHub.Tests.Helpers;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class IssueDetailFetcherTests : TestBase
{
    private const string ISSUE_API_URL = "https://api.github.com/repos/owner/repo/issues/10";

    private const string OPEN_ISSUE_JSON = """
        {
          "number": 10,
          "title": "Test Issue",
          "state": "open",
          "html_url": "https://github.com/owner/repo/issues/10"
        }
        """;

    private const string CLOSED_ISSUE_JSON = """
        {
          "number": 10,
          "title": "Test Issue",
          "state": "closed",
          "html_url": "https://github.com/owner/repo/issues/10"
        }
        """;

    private const string ISSUE_HTML_URL = "https://github.com/owner/repo/issues/10";

    private const string PROXY_BASE_URL = "https://proxy.example/";

    private const string EXPECTED_PROXY_ISSUE_URL = "https://proxy.example/repos/owner/repo/issues/10";

    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IssueDetailFetcher> _logger;
    private readonly IIssueDetailFetcher _fetcher;

    public IssueDetailFetcherTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        this._logger = GetSubstitute<ILogger<IssueDetailFetcher>>();
        this._logger.MockLoggerIsEnabled();
        this._fetcher = new IssueDetailFetcher(httpClientFactory: this._httpClientFactory, logger: this._logger);
    }

    private static HttpClient CreateClient(HttpStatusCode statusCode, string? content = null)
    {
        FixedResponseHandler? handler = new(statusCode: statusCode, content: content);

        try
        {
            HttpClient client = new(handler: handler, disposeHandler: true) { BaseAddress = new Uri(PROXY_BASE_URL) };
            handler = null;

            return client;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    private static HttpClient CreateCapturingClient(CapturingResponseHandler handler, Uri? baseAddress)
    {
        return new HttpClient(handler: handler, disposeHandler: false) { BaseAddress = baseAddress };
    }

    private static GitHubNotification BuildNotification(string type) =>
        CreateNotification(type: type, subjectUrl: new Uri(ISSUE_API_URL));

    private static GitHubNotification BuildNotificationWithNullUrl(string type) =>
        CreateNotification(type: type, subjectUrl: null);

    private static GitHubNotification CreateNotification(string type, Uri? subjectUrl)
    {
        return new GitHubNotification(
            Id: "1",
            Reason: "mention",
            Subject: new NotificationSubject(Title: "Test Issue", Url: subjectUrl, Type: type),
            Repository: new NotificationRepository(
                FullName: "owner/repo",
                Url: new Uri("https://github.com/owner/repo")
            ),
            UpdatedAt: new DateTimeOffset(
                year: 2024,
                month: 1,
                day: 1,
                hour: 0,
                minute: 0,
                second: 0,
                offset: TimeSpan.Zero
            ),
            Unread: true
        );
    }

    [Fact]
    public async Task ReturnsNullWhenSubjectUrlIsNullAsync()
    {
        GitHubNotification notification = BuildNotificationWithNullUrl(type: "Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnsNullForNonIssueTypeAsync()
    {
        GitHubNotification notification = BuildNotification("PullRequest");

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnsNullWhenIssueApiFailsAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnsIssueDetailsForOpenIssueAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OPEN_ISSUE_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: "Open", actual: result.Status);
    }

    [Fact]
    public async Task ReturnsIssueDetailsForClosedIssueAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, CLOSED_ISSUE_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: "Closed", actual: result.Status);
    }

    [Fact]
    public async Task MapsNumberFromIssueAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OPEN_ISSUE_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: 10, actual: result.Number);
    }

    [Fact]
    public async Task MapsTitleFromIssueAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OPEN_ISSUE_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: "Test Issue", actual: result.Title);
    }

    [Fact]
    public async Task MapsHtmlUrlFromIssueAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OPEN_ISSUE_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: new Uri("https://github.com/owner/repo/issues/10"), actual: result.HtmlUrl);
    }

    [Theory]
    [InlineData(ISSUE_API_URL)]
    [InlineData(ISSUE_HTML_URL)]
    [InlineData("http://api.github.com/repos/owner/repo/issues/10/")]
    public async Task RequestsRelativeIssueUrlOnProxyBaseAddressAsync(string subjectUrl)
    {
        using CapturingResponseHandler handler = new(statusCode: HttpStatusCode.OK, content: OPEN_ISSUE_JSON);
        using HttpClient client = CreateCapturingClient(handler: handler, baseAddress: new Uri(PROXY_BASE_URL));
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = CreateNotification(type: "Issue", subjectUrl: new Uri(subjectUrl));

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal(expected: new Uri(EXPECTED_PROXY_ISSUE_URL), actual: handler.CapturedRequest.RequestUri);
    }

    [Fact]
    public async Task LogsWarningWhenIssueApiFailsAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(result);
        Assert.Equal(expected: 1, actual: this._logger.LoggedCount(LogLevel.Warning));
    }

    [Theory]
    [InlineData("https://api.github.com/repos/owner/repo/issues/abc")]
    [InlineData("https://api.github.com/repos/owner/repo/issues/0")]
    [InlineData("https://api.github.com/")]
    public async Task ReturnsNullAndLogsWarningWhenIssueNumberCannotBeDeterminedAsync(string subjectUrl)
    {
        using CapturingResponseHandler handler = new(statusCode: HttpStatusCode.OK, content: OPEN_ISSUE_JSON);
        using HttpClient client = CreateCapturingClient(handler: handler, baseAddress: new Uri(PROXY_BASE_URL));
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = CreateNotification(type: "Issue", subjectUrl: new Uri(subjectUrl));

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(result);
        Assert.Null(handler.CapturedRequest);
        Assert.Equal(expected: 1, actual: this._logger.LoggedCount(LogLevel.Warning));
    }

    [Fact]
    public async Task ReturnsNullAndLogsWarningWhenSubjectUrlIsRelativeAsync()
    {
        using CapturingResponseHandler handler = new(statusCode: HttpStatusCode.OK, content: OPEN_ISSUE_JSON);
        using HttpClient client = CreateCapturingClient(handler: handler, baseAddress: new Uri(PROXY_BASE_URL));
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = CreateNotification(
            type: "Issue",
            subjectUrl: new Uri(uriString: "repos/owner/repo/issues/10", uriKind: UriKind.Relative)
        );

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(result);
        Assert.Null(handler.CapturedRequest);
        Assert.Equal(expected: 1, actual: this._logger.LoggedCount(LogLevel.Warning));
    }

    [Fact]
    public async Task RefusesAndLogsErrorWhenClientHasNoBaseAddressAsync()
    {
        using CapturingResponseHandler handler = new(statusCode: HttpStatusCode.OK, content: OPEN_ISSUE_JSON);
        using HttpClient client = CreateCapturingClient(handler: handler, baseAddress: null);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(result);
        Assert.Null(handler.CapturedRequest);
        Assert.Equal(expected: 1, actual: this._logger.LoggedCount(LogLevel.Error));
    }

    [Fact]
    public async Task RefusesAndLogsErrorWhenRepositoryNameEscapesProxyBaseAddressAsync()
    {
        using CapturingResponseHandler handler = new(statusCode: HttpStatusCode.OK, content: OPEN_ISSUE_JSON);
        using HttpClient client = CreateCapturingClient(
            handler: handler,
            baseAddress: new Uri("https://proxy.example/github/")
        );
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = CreateNotification(type: "Issue", subjectUrl: new Uri(ISSUE_API_URL)) with
        {
            Repository = new NotificationRepository(
                FullName: "../../../owner/repo",
                Url: new Uri("https://github.com/owner/repo")
            ),
        };

        IssueDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(result);
        Assert.Null(handler.CapturedRequest);
        Assert.Equal(expected: 1, actual: this._logger.LoggedCount(LogLevel.Error));
    }

    [Theory]
    [InlineData("//evil.example/repos/owner/repo/issues/10")]
    [InlineData("https://evil.example/repos/owner/repo/issues/10")]
    [InlineData("https://api.github.com/repos/owner/repo/issues/10")]
    [InlineData("http://proxy.example/repos/owner/repo/issues/10")]
    [InlineData("https://proxy.example:8443/repos/owner/repo/issues/10")]
    public static void TryResolveOnBaseAddressRejectsUrlsOffTheBaseAddress(string url)
    {
        bool resolved = IssueDetailFetcher.TryResolveOnBaseAddress(
            baseAddress: new Uri(PROXY_BASE_URL),
            relativeUrl: url,
            out Uri? requestUri
        );

        Assert.False(
            condition: resolved,
            userMessage: "Request URL should be rejected when it is off the base address"
        );
        Assert.Null(requestUri);
    }

    [Fact]
    public static void TryResolveOnBaseAddressRejectsMissingBaseAddress()
    {
        bool resolved = IssueDetailFetcher.TryResolveOnBaseAddress(
            baseAddress: null,
            relativeUrl: "repos/owner/repo/issues/10",
            out Uri? requestUri
        );

        Assert.False(condition: resolved, userMessage: "Request URL should be rejected when there is no base address");
        Assert.Null(requestUri);
    }

    [Fact]
    public static void TryResolveOnBaseAddressAcceptsRelativeUrlOnBaseAddress()
    {
        bool resolved = IssueDetailFetcher.TryResolveOnBaseAddress(
            baseAddress: new Uri(PROXY_BASE_URL),
            relativeUrl: "repos/owner/repo/issues/10",
            out Uri? requestUri
        );

        Assert.True(condition: resolved, userMessage: "Relative request URL should resolve on the base address");
        Assert.Equal(expected: new Uri(EXPECTED_PROXY_ISSUE_URL), actual: requestUri);
    }
}
