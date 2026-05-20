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
    private const string PR_API_URL = "https://api.github.com/repos/owner/repo/pulls/42";

    private const string OPEN_PR_JSON = """
        {
          "data": {
            "repository": {
              "pullRequest": {
                "number": 42,
                "title": "Test PR",
                "state": "OPEN",
                "isDraft": false,
                "url": "https://github.com/owner/repo/pull/42",
                "body": null,
                "headRefOid": "abc123",
                "baseRef": {"name": "main"},
                "assignees": {"nodes": []},
                "labels": {"nodes": []},
                "comments": {"nodes": []},
                "reviews": {"nodes": []}
              }
            }
          }
        }
        """;

    private const string DRAFT_PR_JSON = """
        {
          "data": {
            "repository": {
              "pullRequest": {
                "number": 42,
                "title": "Test PR",
                "state": "OPEN",
                "isDraft": true,
                "url": "https://github.com/owner/repo/pull/42",
                "body": null,
                "headRefOid": "abc123",
                "baseRef": {"name": "main"},
                "assignees": {"nodes": []},
                "labels": {"nodes": []},
                "comments": {"nodes": []},
                "reviews": {"nodes": []}
              }
            }
          }
        }
        """;

    private const string CLOSED_PR_JSON = """
        {
          "data": {
            "repository": {
              "pullRequest": {
                "number": 42,
                "title": "Test PR",
                "state": "CLOSED",
                "isDraft": false,
                "url": "https://github.com/owner/repo/pull/42",
                "body": null,
                "headRefOid": "abc123",
                "baseRef": {"name": "main"},
                "assignees": {"nodes": []},
                "labels": {"nodes": []},
                "comments": {"nodes": []},
                "reviews": {"nodes": []}
              }
            }
          }
        }
        """;

    private const string MERGED_PR_JSON = """
        {
          "data": {
            "repository": {
              "pullRequest": {
                "number": 42,
                "title": "Test PR",
                "state": "MERGED",
                "isDraft": false,
                "url": "https://github.com/owner/repo/pull/42",
                "body": null,
                "headRefOid": "abc123",
                "baseRef": {"name": "main"},
                "assignees": {"nodes": []},
                "labels": {"nodes": []},
                "comments": {"nodes": []},
                "reviews": {"nodes": []}
              }
            }
          }
        }
        """;

    private const string PR_WITH_ASSIGNEES_JSON = """
        {
          "data": {
            "repository": {
              "pullRequest": {
                "number": 42,
                "title": "Test PR",
                "state": "OPEN",
                "isDraft": false,
                "url": "https://github.com/owner/repo/pull/42",
                "body": null,
                "headRefOid": "abc123",
                "baseRef": {"name": "main"},
                "assignees": {"nodes": [{"login": "alice"}, {"login": "bob"}]},
                "labels": {"nodes": []},
                "comments": {"nodes": []},
                "reviews": {"nodes": []}
              }
            }
          }
        }
        """;

    private const string PR_WITH_LABELS_JSON = """
        {
          "data": {
            "repository": {
              "pullRequest": {
                "number": 42,
                "title": "Test PR",
                "state": "OPEN",
                "isDraft": false,
                "url": "https://github.com/owner/repo/pull/42",
                "body": null,
                "headRefOid": "abc123",
                "baseRef": {"name": "main"},
                "assignees": {"nodes": []},
                "labels": {"nodes": [{"name": "bug"}, {"name": "enhancement"}]},
                "comments": {"nodes": []},
                "reviews": {"nodes": []}
              }
            }
          }
        }
        """;

    private const string OPEN_PR_WITH_COMMENT_JSON = """
        {
          "data": {
            "repository": {
              "pullRequest": {
                "number": 42,
                "title": "Test PR",
                "state": "OPEN",
                "isDraft": false,
                "url": "https://github.com/owner/repo/pull/42",
                "body": null,
                "headRefOid": "abc123",
                "baseRef": {"name": "main"},
                "assignees": {"nodes": []},
                "labels": {"nodes": []},
                "comments": {"nodes": [{"body": "A test comment", "author": {"login": "reviewer"}, "url": "https://github.com/owner/repo/issues/42#issuecomment-1", "createdAt": "2024-01-01T00:00:00Z"}]},
                "reviews": {"nodes": []}
              }
            }
          }
        }
        """;

    private const string OPEN_PR_WITH_CHANGES_REQUESTED_REVIEW_JSON = """
        {
          "data": {
            "repository": {
              "pullRequest": {
                "number": 42,
                "title": "Test PR",
                "state": "OPEN",
                "isDraft": false,
                "url": "https://github.com/owner/repo/pull/42",
                "body": null,
                "headRefOid": "abc123",
                "baseRef": {"name": "main"},
                "assignees": {"nodes": []},
                "labels": {"nodes": []},
                "comments": {"nodes": []},
                "reviews": {"nodes": [{"state": "CHANGES_REQUESTED", "body": "Please fix this", "author": {"login": "reviewer"}, "url": "https://github.com/owner/repo/pull/42#pullrequestreview-1", "submittedAt": "2024-01-01T00:00:00Z"}]}
              }
            }
          }
        }
        """;

    private const string OPEN_PR_WITH_APPROVED_REVIEW_JSON = """
        {
          "data": {
            "repository": {
              "pullRequest": {
                "number": 42,
                "title": "Test PR",
                "state": "OPEN",
                "isDraft": false,
                "url": "https://github.com/owner/repo/pull/42",
                "body": null,
                "headRefOid": "abc123",
                "baseRef": {"name": "main"},
                "assignees": {"nodes": []},
                "labels": {"nodes": []},
                "comments": {"nodes": []},
                "reviews": {"nodes": [{"state": "APPROVED", "body": "Looks good", "author": {"login": "reviewer"}, "url": "https://github.com/owner/repo/pull/42#pullrequestreview-1", "submittedAt": "2024-01-01T00:00:00Z"}]}
              }
            }
          }
        }
        """;

    private const string OPEN_PR_WITH_LINKED_ITEMS_JSON = """
        {
          "data": {
            "repository": {
              "pullRequest": {
                "number": 42,
                "title": "Test PR",
                "state": "OPEN",
                "isDraft": false,
                "url": "https://github.com/owner/repo/pull/42",
                "body": "Closes #10\nFixes #11",
                "headRefOid": "abc123",
                "baseRef": {"name": "main"},
                "assignees": {"nodes": []},
                "labels": {"nodes": []},
                "comments": {"nodes": []},
                "reviews": {"nodes": []}
              }
            }
          }
        }
        """;

    private const string WORKFLOW_RUNS_WITH_FAILURE_JSON = """
        {
          "workflow_runs": [{
            "name": "CI",
            "status": "completed",
            "conclusion": "failure",
            "html_url": "https://github.com/owner/repo/actions/runs/123"
          }]
        }
        """;

    private const string WORKFLOW_RUNS_ALL_PASSED_JSON = """
        {
          "workflow_runs": [{
            "name": "CI",
            "status": "completed",
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
            Subject: new NotificationSubject(Title: "Test PR", Url: new Uri(PR_API_URL), Type: type),
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
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, OPEN_PR_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

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
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, DRAFT_PR_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

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
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, CLOSED_PR_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: "Closed", actual: result.Status);
    }

    [Fact]
    public async Task ReturnsPullRequestDetailsForMergedPrAsync()
    {
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, MERGED_PR_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

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
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, OPEN_PR_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

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
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, OPEN_PR_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

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
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, OPEN_PR_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: new Uri("https://github.com/owner/repo/pull/42"), actual: result.HtmlUrl);
    }

    [Fact]
    public async Task MapsAssigneesFromPullRequestAsync()
    {
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, PR_WITH_ASSIGNEES_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

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
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, PR_WITH_LABELS_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

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
    public async Task ReturnsCommentsRegardlessOfNotificationReasonAsync()
    {
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, OPEN_PR_WITH_COMMENT_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Single(result.Comments);
        Assert.Equal(expected: "reviewer", actual: result.Comments[0].Author);
        Assert.Equal(expected: "A test comment", actual: result.Comments[0].Body);
        Assert.Equal(
            expected: new Uri("https://github.com/owner/repo/issues/42#issuecomment-1"),
            actual: result.Comments[0].Url
        );
    }

    [Fact]
    public async Task ReturnsEmptyCommentsWhenCommentListIsEmptyAsync()
    {
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, OPEN_PR_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Empty(result.Comments);
    }

    [Fact]
    public async Task ReturnsAllReviewsRegardlessOfNotificationReasonAsync()
    {
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, OPEN_PR_WITH_CHANGES_REQUESTED_REVIEW_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Single(result.Reviews);
        Assert.Equal(expected: "CHANGES_REQUESTED", actual: result.Reviews[0].State);
        Assert.Equal(expected: "Please fix this", actual: result.Reviews[0].Body);
        Assert.Equal(expected: "reviewer", actual: result.Reviews[0].Author);
    }

    [Fact]
    public async Task ReturnsApprovedReviewInReviewsListAsync()
    {
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, OPEN_PR_WITH_APPROVED_REVIEW_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Single(result.Reviews);
        Assert.Equal(expected: "APPROVED", actual: result.Reviews[0].State);
    }

    [Fact]
    public async Task ReturnsRunsAlwaysNotOnlyForCiActivityAsync()
    {
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, OPEN_PR_JSON);
        using HttpClient runsClient = CreateClient(HttpStatusCode.OK, WORKFLOW_RUNS_WITH_FAILURE_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Single(result.Runs);
        Assert.Equal(expected: "CI", actual: result.Runs[0].Name);
        Assert.Equal(expected: "failure", actual: result.Runs[0].Conclusion);
        Assert.Equal(expected: new Uri("https://github.com/owner/repo/actions/runs/123"), actual: result.Runs[0].Url);
    }

    [Fact]
    public async Task ReturnsSuccessRunsInRunsListAsync()
    {
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, OPEN_PR_JSON);
        using HttpClient runsClient = CreateClient(HttpStatusCode.OK, WORKFLOW_RUNS_ALL_PASSED_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, runsClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Single(result.Runs);
        Assert.Equal(expected: "success", actual: result.Runs[0].Conclusion);
    }

    [Fact]
    public async Task ReturnsEmptyRunsWhenNoRunsExistAsync()
    {
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, OPEN_PR_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Empty(result.Runs);
    }

    [Fact]
    public async Task ParsesLinkedItemsFromBodyAsync()
    {
        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, OPEN_PR_WITH_LINKED_ITEMS_JSON);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Equal(expected: 2, actual: result.LinkedItems.Count);
        Assert.Contains(result.LinkedItems, item => item.Number == 10);
        Assert.Contains(result.LinkedItems, item => item.Number == 11);
    }

    [Fact]
    public async Task TruncatesLongCommentBodyAsync()
    {
        string longBody = new(c: 'x', count: 400);
        string prWithLongComment = $$$"""
            {
              "data": {
                "repository": {
                  "pullRequest": {
                    "number": 42,
                    "title": "Test PR",
                    "state": "OPEN",
                    "isDraft": false,
                    "url": "https://github.com/owner/repo/pull/42",
                    "body": null,
                    "headRefOid": "abc123",
                    "baseRef": {"name": "main"},
                    "assignees": {"nodes": []},
                    "labels": {"nodes": []},
                    "comments": {"nodes": [{"body": "{{{longBody}}}", "author": {"login": "reviewer"}, "url": "https://github.com/owner/repo/issues/42#issuecomment-1", "createdAt": "2024-01-01T00:00:00Z"}]},
                    "reviews": {"nodes": []}
                  }
                }
              }
            }
            """;

        using HttpClient graphQlClient = CreateClient(HttpStatusCode.OK, prWithLongComment);
        using HttpClient notFoundClient = CreateClient(HttpStatusCode.NotFound);
        this._httpClientFactory.CreateClient("GitHub").Returns(graphQlClient, notFoundClient);

        GitHubNotification notification = BuildNotification(type: "PullRequest", reason: "mention");

        PullRequestDetails? result = await this._fetcher.FetchAsync(
            notification: notification,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.Single(result.Comments);
        Assert.Equal(expected: 301, actual: result.Comments[0].Body.Length);
        Assert.True(result.Comments[0].Body.EndsWith('…'), userMessage: "Expected body to end with ellipsis");
    }
}
