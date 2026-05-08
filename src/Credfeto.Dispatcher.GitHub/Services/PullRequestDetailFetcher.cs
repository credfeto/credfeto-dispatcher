using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Models;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class PullRequestDetailFetcher : IPullRequestDetailFetcher
{
    private const string PullRequestType = "PullRequest";
    private const string CommentReason = "comment";
    private const string ReviewRequestedReason = "review_requested";
    private const string ChangesRequestedState = "CHANGES_REQUESTED";
    private const string CiActivityReason = "ci_activity";
    private const int MaxBodyLength = 300;

    private const string PullRequestQuery = """
        query PullRequestDetails($owner: String!, $repo: String!, $number: Int!) {
          repository(owner: $owner, name: $repo) {
            pullRequest(number: $number) {
              number
              title
              state
              isDraft
              url
              headRefOid
              baseRefOid
              baseRef {
                target {
                  oid
                }
              }
              assignees(first: 10) {
                nodes {
                  login
                }
              }
              labels(first: 10) {
                nodes {
                  name
                }
              }
              comments(last: 1) {
                nodes {
                  body
                  author {
                    login
                  }
                  url
                }
              }
              reviews(last: 10, states: [CHANGES_REQUESTED]) {
                nodes {
                  state
                  body
                  author {
                    login
                  }
                  url
                }
              }
            }
          }
        }
        """;

    private readonly IHttpClientFactory _httpClientFactory;

    public PullRequestDetailFetcher(IHttpClientFactory httpClientFactory)
    {
        this._httpClientFactory = httpClientFactory;
    }

    public async ValueTask<PullRequestDetails?> FetchAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    )
    {
        if (
            !string.Equals(
                a: notification.Subject.Type,
                b: PullRequestType,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return null;
        }

        GraphQlPullRequestData? pr = await this.FetchPullRequestViaGraphQlAsync(
            subjectUrl: notification.Subject.Url,
            cancellationToken: cancellationToken
        );

        if (pr is null)
        {
            return null;
        }

        (string? failedRunName, Uri? failedRunUrl) = await this.FetchFailedRunAsync(
            notification: notification,
            headSha: pr.HeadRefOid,
            cancellationToken: cancellationToken
        );

        (string? commentBody, string? commentAuthor, Uri? commentUrl) = ExtractComment(
            notification: notification,
            pr: pr
        );
        (string? reviewState, string? reviewBody, string? reviewAuthor, Uri? reviewUrl) =
            ExtractReview(notification: notification, pr: pr);

        return new PullRequestDetails(
            Number: pr.Number,
            Title: pr.Title,
            Status: DetermineStatus(pr),
            HtmlUrl: new Uri(pr.Url),
            Assignees: [.. pr.Assignees?.Nodes?.Select(u => u.Login) ?? []],
            Labels: [.. pr.Labels?.Nodes?.Select(l => l.Name) ?? []],
            CommentBody: commentBody,
            CommentAuthor: commentAuthor,
            CommentUrl: commentUrl,
            ReviewState: reviewState,
            ReviewBody: reviewBody,
            ReviewAuthor: reviewAuthor,
            ReviewUrl: reviewUrl,
            FailedRunName: failedRunName,
            FailedRunUrl: failedRunUrl,
            IsUpToDate: DetermineIsUpToDate(baseRefOid: pr.BaseRefOid, baseRef: pr.BaseRef)
        );
    }

    private async ValueTask<GraphQlPullRequestData?> FetchPullRequestViaGraphQlAsync(
        Uri subjectUrl,
        CancellationToken cancellationToken
    )
    {
        (string owner, string repo, int number) = ParsePullRequestUrl(subjectUrl);

        GraphQlRequest request = new(
            Query: PullRequestQuery,
            Variables: new GraphQlVariables(Owner: owner, Repo: repo, Number: number)
        );

        HttpClient httpClient = this._httpClientFactory.CreateClient("GitHub");

        using HttpRequestMessage message = new(method: HttpMethod.Post, requestUri: "graphql")
        {
            Content = JsonContent.Create(
                inputValue: request,
                jsonTypeInfo: NotificationSerializerContext.Default.GraphQlRequest
            ),
        };

        using HttpResponseMessage response = await httpClient.SendAsync(
            request: message,
            cancellationToken: cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        GraphQlResponse? gqlResponse = JsonSerializer.Deserialize(
            json: json,
            jsonTypeInfo: NotificationSerializerContext.Default.GraphQlResponse
        );

        return gqlResponse?.Data?.Repository?.PullRequest;
    }

    private async ValueTask<(string? name, Uri? url)> FetchFailedRunAsync(
        GitHubNotification notification,
        string headSha,
        CancellationToken cancellationToken
    )
    {
        if (
            !string.Equals(
                a: notification.Reason,
                b: CiActivityReason,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return (null, null);
        }

        string runsUrl = BuildWorkflowRunsUrl(
            repoFullName: notification.Repository.FullName,
            headSha: headSha
        );
        ApiWorkflowRunsResponse? runsResponse = await this.GetAsync<ApiWorkflowRunsResponse>(
            url: runsUrl,
            jsonTypeInfo: NotificationSerializerContext.Default.ApiWorkflowRunsResponse,
            cancellationToken: cancellationToken
        );
        ApiWorkflowRun? failedRun = runsResponse?.WorkflowRuns.FirstOrDefault(r =>
            string.Equals(
                a: r.Conclusion,
                b: "failure",
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        );

        if (failedRun is null)
        {
            return (null, null);
        }

        return (failedRun.Name, new Uri(failedRun.HtmlUrl));
    }

    private static (string? body, string? author, Uri? url) ExtractComment(
        GitHubNotification notification,
        GraphQlPullRequestData pr
    )
    {
        if (
            !string.Equals(
                a: notification.Reason,
                b: CommentReason,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return (null, null, null);
        }

        GraphQlCommentNode? latest = pr.Comments?.Nodes?.LastOrDefault();

        if (latest is null)
        {
            return (null, null, null);
        }

        return (TruncateBody(latest.Body), latest.Author?.Login, new Uri(latest.Url));
    }

    private static (string? state, string? body, string? author, Uri? url) ExtractReview(
        GitHubNotification notification,
        GraphQlPullRequestData pr
    )
    {
        if (
            !string.Equals(
                a: notification.Reason,
                b: ReviewRequestedReason,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return (null, null, null, null);
        }

        GraphQlReviewNode? changesRequested = pr.Reviews?.Nodes?.LastOrDefault(r =>
            string.Equals(
                a: r.State,
                b: ChangesRequestedState,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        );

        if (changesRequested is null)
        {
            return (null, null, null, null);
        }

        return (
            changesRequested.State,
            TruncateBody(changesRequested.Body),
            changesRequested.Author?.Login,
            new Uri(changesRequested.Url)
        );
    }

    private static bool? DetermineIsUpToDate(string baseRefOid, GraphQlRefNode? baseRef)
    {
        if (baseRef?.Target is null || string.IsNullOrEmpty(baseRefOid))
        {
            return null;
        }

        return string.Equals(
            a: baseRef.Target.Oid,
            b: baseRefOid,
            comparisonType: StringComparison.Ordinal
        );
    }

    private static string DetermineStatus(GraphQlPullRequestData pr)
    {
        if (
            string.Equals(
                a: pr.State,
                b: "CLOSED",
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return "Closed";
        }

        return pr.IsDraft ? "Draft" : "Open";
    }

    private static (string owner, string repo, int number) ParsePullRequestUrl(Uri url)
    {
        string[] segments = url.AbsolutePath.Split('/');

        return (segments[2], segments[3], int.Parse(segments[5], CultureInfo.InvariantCulture));
    }

    private static string BuildWorkflowRunsUrl(string repoFullName, string headSha)
    {
        return $"repos/{repoFullName}/actions/runs?head_sha={headSha}&per_page=10";
    }

    private static string TruncateBody(string body)
    {
        return body.Length <= MaxBodyLength ? body : body[..MaxBodyLength] + "…";
    }

    private async ValueTask<T?> GetAsync<T>(
        string url,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken
    )
        where T : class
    {
        HttpClient httpClient = this._httpClientFactory.CreateClient("GitHub");

        using HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: url);
        using HttpResponseMessage response = await httpClient.SendAsync(
            request: request,
            cancellationToken: cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize(json: json, jsonTypeInfo: jsonTypeInfo);
    }
}
