using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using Credfeto.Dispatcher.GitHub.Tests.Helpers;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class IssueDetailFetcherTests : TestBase
{
    private const string IssueApiUrl = "https://api.github.com/repos/owner/repo/issues/10";

    private const string OpenIssueJson =
        """
        {
          "number": 10,
          "title": "Test Issue",
          "state": "open",
          "html_url": "https://github.com/owner/repo/issues/10",
          "labels": []
        }
        """;

    private const string ClosedIssueJson =
        """
        {
          "number": 10,
          "title": "Test Issue",
          "state": "closed",
          "html_url": "https://github.com/owner/repo/issues/10",
          "labels": []
        }
        """;

    private const string IssueWithUrgentLabelJson =
        """
        {
          "number": 10,
          "title": "Test Issue",
          "state": "open",
          "html_url": "https://github.com/owner/repo/issues/10",
          "labels": [{"name": "Urgent"}]
        }
        """;

    private const string IssueWithHighLabelJson =
        """
        {
          "number": 10,
          "title": "Test Issue",
          "state": "open",
          "html_url": "https://github.com/owner/repo/issues/10",
          "labels": [{"name": "High"}]
        }
        """;

    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly IIssueDetailFetcher _fetcher;

    public IssueDetailFetcherTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        this._fetcher = new IssueDetailFetcher(this._httpClientFactory);
    }

    private static HttpClient CreateClient(HttpStatusCode statusCode, string? content = null)
    {
        FixedResponseHandler? handler = new(statusCode: statusCode, content: content);

        try
        {
            HttpClient client = new(handler: handler, disposeHandler: true) { BaseAddress = new Uri("https://api.github.com/") };
            handler = null;

            return client;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    private static GitHubNotification BuildNotification(string type)
    {
        return new GitHubNotification(
            Id: "1",
            Reason: "mention",
            Subject: new NotificationSubject(Title: "Test Issue", Url: new Uri(IssueApiUrl), Type: type),
            Repository: new NotificationRepository(FullName: "owner/repo", Url: new Uri("https://github.com/owner/repo")),
            UpdatedAt: new DateTimeOffset(year: 2024, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero),
            Unread: true);
    }

    [Fact]
    public async Task ReturnsNullForNonIssueTypeAsync()
    {
        GitHubNotification notification = BuildNotification("PullRequest");

        IssueDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnsNullWhenIssueApiFailsAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnsIssueDetailsForOpenIssueAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OpenIssueJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "Open", actual: result.Status);
    }

    [Fact]
    public async Task ReturnsIssueDetailsForClosedIssueAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, ClosedIssueJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "Closed", actual: result.Status);
    }

    [Fact]
    public async Task MapsNumberFromIssueAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OpenIssueJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: 10, actual: result.Number);
    }

    [Fact]
    public async Task MapsTitleFromIssueAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OpenIssueJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "Test Issue", actual: result.Title);
    }

    [Fact]
    public async Task MapsHtmlUrlFromIssueAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OpenIssueJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: new Uri("https://github.com/owner/repo/issues/10"), actual: result.HtmlUrl);
    }

    [Fact]
    public async Task MapsRepositoryFromNotificationAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OpenIssueJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "owner", actual: result.Repository.Owner);
        Assert.Equal(expected: "repo", actual: result.Repository.Name);
        Assert.Equal(expected: new Uri("https://github.com/owner/repo"), actual: result.Repository.Url);
    }

    [Fact]
    public async Task MapsLastNotificationFromNotificationAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OpenIssueJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "1", actual: result.LastNotification.Id);
        Assert.Equal(
            expected: new DateTimeOffset(year: 2024, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero),
            actual: result.LastNotification.Timestamp);
    }

    [Fact]
    public async Task DefaultsPriorityToUnknownWhenNoLabelsAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OpenIssueJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "Unknown", actual: result.Priority);
    }

    [Fact]
    public async Task SetsPriorityToUrgentFromLabelAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, IssueWithUrgentLabelJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "Urgent", actual: result.Priority);
    }

    [Fact]
    public async Task SetsPriorityToHighFromLabelAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, IssueWithHighLabelJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification("Issue");

        IssueDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "High", actual: result.Priority);
    }
}
