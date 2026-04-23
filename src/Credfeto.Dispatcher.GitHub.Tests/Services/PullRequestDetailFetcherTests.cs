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

    private const string OpenPrJson =
        """
        {
          "number": 42,
          "title": "Test PR",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [],
          "labels": [],
          "head": {"sha": "abc123"},
          "base": {"ref": "main"}
        }
        """;

    private const string DraftPrJson =
        """
        {
          "number": 42,
          "title": "Test PR",
          "state": "open",
          "draft": true,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [],
          "labels": [],
          "head": {"sha": "abc123"},
          "base": {"ref": "main"}
        }
        """;

    private const string ClosedPrJson =
        """
        {
          "number": 42,
          "title": "Test PR",
          "state": "closed",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [],
          "labels": [],
          "head": {"sha": "abc123"},
          "base": {"ref": "main"}
        }
        """;

    private const string PrWithAssigneesJson =
        """
        {
          "number": 42,
          "title": "Test PR",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [{"login": "alice"}, {"login": "bob"}],
          "labels": [],
          "head": {"sha": "abc123"},
          "base": {"ref": "main"}
        }
        """;

    private const string PrWithLabelsJson =
        """
        {
          "number": 42,
          "title": "Test PR",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [],
          "labels": [{"name": "bug"}, {"name": "enhancement"}],
          "head": {"sha": "abc123"},
          "base": {"ref": "main"}
        }
        """;

    private const string CommentsJson =
        """
        [{
          "body": "A test comment",
          "user": {"login": "reviewer"},
          "html_url": "https://github.com/owner/repo/issues/42#issuecomment-1",
          "created_at": "2024-01-01T00:00:00Z"
        }]
        """;

    private const string ReviewsWithChangesRequestedJson =
        """
        [{
          "state": "CHANGES_REQUESTED",
          "body": "Please fix this",
          "user": {"login": "reviewer"},
          "html_url": "https://github.com/owner/repo/pull/42#pullrequestreview-1",
          "submitted_at": "2024-01-01T00:00:00Z"
        }]
        """;

    private const string ReviewsApprovedJson =
        """
        [{
          "state": "APPROVED",
          "body": "Looks good",
          "user": {"login": "reviewer"},
          "html_url": "https://github.com/owner/repo/pull/42#pullrequestreview-1",
          "submitted_at": "2024-01-01T00:00:00Z"
        }]
        """;

    private const string WorkflowRunsWithFailureJson =
        """
        {
          "workflow_runs": [{
            "name": "CI",
            "status": "completed",
            "conclusion": "failure",
            "html_url": "https://github.com/owner/repo/actions/runs/123"
          }]
        }
        """;

    private const string WorkflowRunsAllPassedJson =
        """
        {
          "workflow_runs": [{
            "name": "CI",
            "status": "completed",
            "conclusion": "success",
            "html_url": "https://github.com/owner/repo/actions/runs/123"
          }]
        }
        """;

    private const string RequiredChecksWithCiJson =
        """
        {
          "contexts": ["CI"]
        }
        """;

    private const string OpenPrWithBodyJson =
        """
        {
          "number": 42,
          "title": "Test PR",
          "body": "Closes #10",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [],
          "labels": [],
          "head": {"sha": "abc123"},
          "base": {"ref": "main"}
        }
        """;

    private const string EmptyArrayJson = "[]";
    private const string EmptyRunsJson = """{"workflow_runs": []}""";

    private const string PrWithHighPriorityLabelJson =
        """
        {
          "number": 42,
          "title": "Test PR",
          "body": null,
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [],
          "labels": [{"name": "High"}],
          "head": {"sha": "abc123"},
          "base": {"ref": "main"}
        }
        """;

    private const string PrWithUrgentPriorityLabelJson =
        """
        {
          "number": 42,
          "title": "Test PR",
          "body": null,
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/42",
          "assignees": [],
          "labels": [{"name": "Urgent"}],
          "head": {"sha": "abc123"},
          "base": {"ref": "main"}
        }
        """;

    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly IPullRequestDetailFetcher _fetcher;

    public PullRequestDetailFetcherTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        GitHub.Configuration.GitHubFilterOptions filterOptions = new()
        {
            NoWorkFilter = []
        };
        this._fetcher = new PullRequestDetailFetcher(this._httpClientFactory, filterOptions);
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

    private static HttpClient CreateNotFoundClient() => CreateClient(HttpStatusCode.NotFound);

    private static HttpClient CreateEmptyArrayClient() => CreateClient(HttpStatusCode.OK, EmptyArrayJson);

    private static HttpClient CreateEmptyRunsClient() => CreateClient(HttpStatusCode.OK, EmptyRunsJson);

    private static GitHubNotification BuildNotification(string type, string reason)
    {
        return new GitHubNotification(
            Id: "1",
            Reason: reason,
            Subject: new NotificationSubject(Title: "Test PR", Url: new Uri(PrApiUrl), Type: type),
            Repository: new NotificationRepository(FullName: "owner/repo", Url: new Uri("https://github.com/owner/repo")),
            UpdatedAt: new DateTimeOffset(year: 2024, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero),
            Unread: true);
    }

    [Fact]
    public async Task ReturnsNullForNonPullRequestTypeAsync()
    {
        GitHubNotification notification = BuildNotification(type: "Issue", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnsNullWhenPullRequestApiFailsAsync()
    {
        using HttpClient client = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnsPullRequestDetailsForOpenPrAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "Open", actual: result.Status);
    }

    [Fact]
    public async Task ReturnsPullRequestDetailsForDraftPrAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, DraftPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "Draft", actual: result.Status);
    }

    [Fact]
    public async Task ReturnsPullRequestDetailsForClosedPrAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, ClosedPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "Closed", actual: result.Status);
    }

    [Fact]
    public async Task MapsTitleFromPullRequestAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "Test PR", actual: result.Title);
    }

    [Fact]
    public async Task MapsNumberFromPullRequestAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: 42, actual: result.Number);
    }

    [Fact]
    public async Task MapsHtmlUrlFromPullRequestAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: new Uri("https://github.com/owner/repo/pull/42"), actual: result.HtmlUrl);
    }

    [Fact]
    public async Task MapsAssigneesFromPullRequestAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PrWithAssigneesJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: 2, actual: result.Assignees.Count);
        Assert.Contains(expected: "alice", collection: result.Assignees);
        Assert.Contains(expected: "bob", collection: result.Assignees);
    }

    [Fact]
    public async Task MapsLabelsFromPullRequestAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PrWithLabelsJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: 2, actual: result.Labels.Count);
        Assert.Contains(expected: "bug", collection: result.Labels);
        Assert.Contains(expected: "enhancement", collection: result.Labels);
    }

    [Fact]
    public async Task FetchesCommentsListAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateClient(HttpStatusCode.OK, CommentsJson);
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "comment");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        PullRequestComment comment = Assert.Single(result.Comments);
        Assert.Equal(expected: "A test comment", actual: comment.Body);
        Assert.Equal(expected: "reviewer", actual: comment.Author);
        Assert.Equal(expected: new Uri("https://github.com/owner/repo/issues/42#issuecomment-1"), actual: comment.Url);
    }

    [Fact]
    public async Task ReturnsEmptyCommentsWhenApiFailsAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateClient(HttpStatusCode.InternalServerError);
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "comment");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Empty(result.Comments);
    }

    [Fact]
    public async Task ReturnsEmptyCommentsWhenListIsEmptyAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "comment");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Empty(result.Comments);
    }

    [Fact]
    public async Task FetchesReviewsListAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateClient(HttpStatusCode.OK, ReviewsWithChangesRequestedJson);
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "review_requested");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        PullRequestReview review = Assert.Single(result.Reviews);
        Assert.Equal(expected: "CHANGES_REQUESTED", actual: review.State);
        Assert.Equal(expected: "Please fix this", actual: review.Body);
        Assert.Equal(expected: "reviewer", actual: review.Author);
    }

    [Fact]
    public async Task FetchesApprovedReviewInListAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateClient(HttpStatusCode.OK, ReviewsApprovedJson);
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "review_requested");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        PullRequestReview approvedReview = Assert.Single(result.Reviews);
        Assert.Equal(expected: "APPROVED", actual: approvedReview.State);
    }

    [Fact]
    public async Task FetchesRunsListWithFailureAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateClient(HttpStatusCode.OK, WorkflowRunsWithFailureJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "ci_activity");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        PullRequestRun failedRun = Assert.Single(result.Runs);
        Assert.Equal(expected: "CI", actual: failedRun.Name);
        Assert.Equal(expected: "failure", actual: failedRun.Conclusion);
        Assert.Equal(expected: new Uri("https://github.com/owner/repo/actions/runs/123"), actual: failedRun.Url);
    }

    [Fact]
    public async Task FetchesRunsListWithPassedRunAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateClient(HttpStatusCode.OK, WorkflowRunsAllPassedJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "ci_activity");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        PullRequestRun passedRun = Assert.Single(result.Runs);
        Assert.Equal(expected: "success", actual: passedRun.Conclusion);
    }

    [Fact]
    public async Task SetsIsRequiredTrueWhenRunMatchesRequiredContextAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateClient(HttpStatusCode.OK, RequiredChecksWithCiJson);
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateClient(HttpStatusCode.OK, WorkflowRunsWithFailureJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "ci_activity");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        PullRequestRun requiredRun = Assert.Single(result.Runs);
        Assert.True(requiredRun.IsRequired, userMessage: "Expected run to be marked as required");
    }

    [Fact]
    public async Task MapsRepositoryFromNotificationAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "owner", actual: result.Repository.Owner);
        Assert.Equal(expected: "repo", actual: result.Repository.Name);
        Assert.Equal(expected: new Uri("https://github.com/owner/repo"), actual: result.Repository.Url);
    }

    [Fact]
    public async Task MapsLastNotificationFromNotificationAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "1", actual: result.LastNotification.Id);
        Assert.Equal(
            expected: new DateTimeOffset(year: 2024, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero),
            actual: result.LastNotification.Timestamp);
    }

    [Fact]
    public async Task MapsPrBodyAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrWithBodyJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "Closes #10", actual: result.Body);
    }

    [Fact]
    public async Task ParsesLinkedItemsFromPrBodyAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrWithBodyJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        LinkedItem linkedItem = Assert.Single(result.LinkedItems);
        Assert.Equal(expected: 10, actual: linkedItem.Number);
    }

    [Fact]
    public async Task TruncatesLongCommentBodyAsync()
    {
        string longBody = new(c: 'x', count: 400);
        string commentsWithLongBody = $$"""
                                        [{
                                          "body": "{{longBody}}",
                                          "user": {"login": "reviewer"},
                                          "html_url": "https://github.com/owner/repo/issues/42#issuecomment-1",
                                          "created_at": "2024-01-01T00:00:00Z"
                                        }]
                                        """;

        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateClient(HttpStatusCode.OK, commentsWithLongBody);
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "comment");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        PullRequestComment truncatedComment = Assert.Single(result.Comments);
        Assert.Equal(expected: 301, actual: truncatedComment.Body.Length);
        Assert.True(truncatedComment.Body.EndsWith('\u2026'), userMessage: "Expected body to end with ellipsis");
    }

    [Fact]
    public async Task DefaultsPriorityToUnknownWhenNoLabelsAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "Unknown", actual: result.Priority);
    }

    [Fact]
    public async Task SetsPriorityToHighFromLabelAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PrWithHighPriorityLabelJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "High", actual: result.Priority);
    }

    [Fact]
    public async Task SetsPriorityToUrgentFromLabelAsync()
    {
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PrWithUrgentPriorityLabelJson);
        using HttpClient reqChecksClient = CreateNotFoundClient();
        using HttpClient commentsClient = CreateEmptyArrayClient();
        using HttpClient reviewsClient = CreateEmptyArrayClient();
        using HttpClient runsClient = CreateEmptyRunsClient();
        this._httpClientFactory.CreateClient("GitHub").Returns(prClient, reqChecksClient, commentsClient, reviewsClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(notification: notification, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: "Urgent", actual: result.Priority);
    }
}
