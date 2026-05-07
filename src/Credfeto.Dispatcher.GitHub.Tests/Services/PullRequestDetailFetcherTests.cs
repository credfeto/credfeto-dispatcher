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

public sealed class PullRequestDetailFetcherTests : TestBase
{
    private const string PrApiUrl = "https://api.github.com/repos/owner/repo/pulls/42";

    private const string OpenPrJson = """
        {
          "number": 42,
          "title": "Test PR",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [],
          "labels": [],
          "head": {"sha": "abc123"}
        }
        """;

    private const string DraftPrJson = """
        {
          "number": 42,
          "title": "Test PR",
          "state": "open",
          "draft": true,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [],
          "labels": [],
          "head": {"sha": "abc123"}
        }
        """;

    private const string ClosedPrJson = """
        {
          "number": 42,
          "title": "Test PR",
          "state": "closed",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [],
          "labels": [],
          "head": {"sha": "abc123"}
        }
        """;

    private const string PrWithAssigneesJson = """
        {
          "number": 42,
          "title": "Test PR",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [{"login": "alice"}, {"login": "bob"}],
          "labels": [],
          "head": {"sha": "abc123"}
        }
        """;

    private const string PrWithLabelsJson = """
        {
          "number": 42,
          "title": "Test PR",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [],
          "labels": [{"name": "bug"}, {"name": "enhancement"}],
          "head": {"sha": "abc123"}
        }
        """;

    private const string CommentsJson = """
        [{
          "body": "A test comment",
          "user": {"login": "reviewer"},
          "html_url": "https://github.com/owner/repo/issues/42#issuecomment-1"
        }]
        """;

    private const string ReviewsWithChangesRequestedJson = """
        [{
          "state": "CHANGES_REQUESTED",
          "body": "Please fix this",
          "user": {"login": "reviewer"},
          "html_url": "https://github.com/owner/repo/pull/42#pullrequestreview-1"
        }]
        """;

    private const string ReviewsApprovedJson = """
        [{
          "state": "APPROVED",
          "body": "Looks good",
          "user": {"login": "reviewer"},
          "html_url": "https://github.com/owner/repo/pull/42#pullrequestreview-1"
        }]
        """;

    private const string WorkflowRunsWithFailureJson = """
        {
          "workflow_runs": [{
            "name": "CI",
            "conclusion": "failure",
            "html_url": "https://github.com/owner/repo/actions/runs/123"
          }]
        }
        """;

    private const string WorkflowRunsAllPassedJson = """
        {
          "workflow_runs": [{
            "name": "CI",
            "conclusion": "success",
            "html_url": "https://github.com/owner/repo/actions/runs/123"
          }]
        }
        """;

    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly IPullRequestDetailFetcher _fetcher;

    public PullRequestDetailFetcherTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        this._fetcher = new PullRequestDetailFetcher(this._httpClientFactory);
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

    private static GitHubNotification BuildNotification(string type, string reason)
    {
        return new GitHubNotification(
            Id: "1",
            Reason: reason,
            Subject: new NotificationSubject(Title: "Test PR", Url: new Uri(PrApiUrl), Type: type),
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
    public async Task ReturnsNullForNonPullRequestTypeAsync()
    {
        GitHubNotification notification = BuildNotification(type: "Issue", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnsNullWhenPullRequestApiFailsAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnsPullRequestDetailsForOpenPrAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OpenPrJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: "Open", actual: result.Status);
    }

    [Fact]
    public async Task ReturnsPullRequestDetailsForDraftPrAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, DraftPrJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: "Draft", actual: result.Status);
    }

    [Fact]
    public async Task ReturnsPullRequestDetailsForClosedPrAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, ClosedPrJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: "Closed", actual: result.Status);
    }

    [Fact]
    public async Task MapsTitleFromPullRequestAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OpenPrJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: "Test PR", actual: result.Title);
    }

    [Fact]
    public async Task MapsNumberFromPullRequestAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OpenPrJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: 42, actual: result.Number);
    }

    [Fact]
    public async Task MapsHtmlUrlFromPullRequestAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, OpenPrJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(
            expected: new Uri("https://github.com/owner/repo/pull/42"),
            actual: result.HtmlUrl
        );
    }

    [Fact]
    public async Task MapsAssigneesFromPullRequestAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, PrWithAssigneesJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: 2, actual: result.Assignees.Count);
        Assert.Contains(expected: "alice", collection: result.Assignees);
        Assert.Contains(expected: "bob", collection: result.Assignees);
    }

    [Fact]
    public async Task MapsLabelsFromPullRequestAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.OK, PrWithLabelsJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: 2, actual: result.Labels.Count);
        Assert.Contains(expected: "bug", collection: result.Labels);
        Assert.Contains(expected: "enhancement", collection: result.Labels);
    }

    [Fact]
    public async Task FetchesLatestCommentWhenReasonIsCommentAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient commentClient = CreateClient(HttpStatusCode.OK, CommentsJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, commentClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "comment");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: "A test comment", actual: result.CommentBody);
        Assert.Equal(expected: "reviewer", actual: result.CommentAuthor);
        Assert.Equal(
            expected: new Uri("https://github.com/owner/repo/issues/42#issuecomment-1"),
            actual: result.CommentUrl
        );
    }

    [Fact]
    public async Task ReturnsNullCommentWhenCommentApiFailsAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient failClient = CreateClient(HttpStatusCode.InternalServerError);
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, failClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "comment");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Null(result.CommentBody);
        Assert.Null(result.CommentAuthor);
        Assert.Null(result.CommentUrl);
    }

    [Fact]
    public async Task ReturnsNullCommentWhenCommentListIsEmptyAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient emptyClient = CreateClient(HttpStatusCode.OK, "[]");
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, emptyClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "comment");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Null(result.CommentBody);
    }

    [Fact]
    public async Task FetchesReviewWhenReasonIsReviewRequestedAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reviewClient = CreateClient(
            HttpStatusCode.OK,
            ReviewsWithChangesRequestedJson
        );
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reviewClient);

        GitHubNotification notification = BuildNotification(
            type: "PullRequest",
            reason: "review_requested"
        );

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: "CHANGES_REQUESTED", actual: result.ReviewState);
        Assert.Equal(expected: "Please fix this", actual: result.ReviewBody);
        Assert.Equal(expected: "reviewer", actual: result.ReviewAuthor);
    }

    [Fact]
    public async Task ReturnsNullReviewWhenNoChangesRequestedAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reviewClient = CreateClient(HttpStatusCode.OK, ReviewsApprovedJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reviewClient);

        GitHubNotification notification = BuildNotification(
            type: "PullRequest",
            reason: "review_requested"
        );

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Null(result.ReviewState);
        Assert.Null(result.ReviewBody);
    }

    [Fact]
    public async Task FetchesFailedRunWhenReasonIsCiActivityAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient runsClient = CreateClient(HttpStatusCode.OK, WorkflowRunsWithFailureJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, runsClient);

        GitHubNotification notification = BuildNotification(
            type: "PullRequest",
            reason: "ci_activity"
        );

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: "CI", actual: result.FailedRunName);
        Assert.Equal(
            expected: new Uri("https://github.com/owner/repo/actions/runs/123"),
            actual: result.FailedRunUrl
        );
    }

    [Fact]
    public async Task ReturnsNullFailedRunWhenAllRunsPassedAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient runsClient = CreateClient(HttpStatusCode.OK, WorkflowRunsAllPassedJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, runsClient);

        GitHubNotification notification = BuildNotification(
            type: "PullRequest",
            reason: "ci_activity"
        );

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Null(result.FailedRunName);
        Assert.Null(result.FailedRunUrl);
    }

    [Fact]
    public async Task TruncatesLongCommentBodyAsync()
    {
        string longBody = new(c: 'x', count: 400);
        string commentsWithLongBody = $$"""
            [{
              "body": "{{longBody}}",
              "user": {"login": "reviewer"},
              "html_url": "https://github.com/owner/repo/issues/42#issuecomment-1"
            }]
            """;

        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient commentClient = CreateClient(HttpStatusCode.OK, commentsWithLongBody);
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, commentClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "comment");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.NotNull(result.CommentBody);
        Assert.Equal(expected: 301, actual: result.CommentBody.Length);
        Assert.True(
            result.CommentBody.EndsWith('…'),
            userMessage: "Expected body to end with ellipsis"
        );
    }
}
