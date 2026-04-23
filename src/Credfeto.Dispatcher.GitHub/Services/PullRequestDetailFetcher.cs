using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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

    private readonly IHttpClientFactory _httpClientFactory;

    public PullRequestDetailFetcher(IHttpClientFactory httpClientFactory)
    {
        this._httpClientFactory = httpClientFactory;
    }

    public async ValueTask<PullRequestDetails?> FetchAsync(GitHubNotification notification, CancellationToken cancellationToken)
    {
        if (!string.Equals(a: notification.Subject.Type, b: PullRequestType, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string apiUrl = notification.Subject.Url.ToString();
        ApiPullRequest? pr = await this.GetAsync<ApiPullRequest>(url: apiUrl, jsonTypeInfo: NotificationSerializerContext.Default.ApiPullRequest, cancellationToken: cancellationToken);

        if (pr is null)
        {
            return null;
        }

        (string? commentBody, string? commentAuthor, Uri? commentUrl) = await this.FetchCommentAsync(notification: notification, apiUrl: apiUrl, cancellationToken: cancellationToken);
        (string? reviewState, string? reviewBody, string? reviewAuthor, Uri? reviewUrl) = await this.FetchReviewAsync(notification: notification, apiUrl: apiUrl, cancellationToken: cancellationToken);
        (string? failedRunName, Uri? failedRunUrl) = await this.FetchFailedRunAsync(notification: notification, pr: pr, cancellationToken: cancellationToken);

        return new PullRequestDetails(
            Number: pr.Number,
            Title: pr.Title,
            Status: DetermineStatus(pr),
            HtmlUrl: new Uri(pr.HtmlUrl),
            Assignees: [..pr.Assignees.Select(u => u.Login)],
            Labels: [..pr.Labels.Select(l => l.Name)],
            CommentBody: commentBody,
            CommentAuthor: commentAuthor,
            CommentUrl: commentUrl,
            ReviewState: reviewState,
            ReviewBody: reviewBody,
            ReviewAuthor: reviewAuthor,
            ReviewUrl: reviewUrl,
            FailedRunName: failedRunName,
            FailedRunUrl: failedRunUrl
        );
    }

    private async ValueTask<(string? body, string? author, Uri? url)> FetchCommentAsync(GitHubNotification notification, string apiUrl, CancellationToken cancellationToken)
    {
        if (!string.Equals(a: notification.Reason, b: CommentReason, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return (null, null, null);
        }

        string commentsUrl = BuildCommentsUrl(apiUrl);
        ApiIssueComment[]? comments = await this.GetAsync<ApiIssueComment[]>(url: commentsUrl, jsonTypeInfo: NotificationSerializerContext.Default.ApiIssueCommentArray, cancellationToken: cancellationToken);
        ApiIssueComment? latest = comments?.LastOrDefault();

        if (latest is null)
        {
            return (null, null, null);
        }

        return (TruncateBody(latest.Body), latest.User.Login, new Uri(latest.HtmlUrl));
    }

    private async ValueTask<(string? state, string? body, string? author, Uri? url)> FetchReviewAsync(GitHubNotification notification, string apiUrl, CancellationToken cancellationToken)
    {
        if (!string.Equals(a: notification.Reason, b: ReviewRequestedReason, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return (null, null, null, null);
        }

        string reviewsUrl = BuildReviewsUrl(apiUrl);
        ApiPullRequestReview[]? reviews = await this.GetAsync<ApiPullRequestReview[]>(url: reviewsUrl, jsonTypeInfo: NotificationSerializerContext.Default.ApiPullRequestReviewArray, cancellationToken: cancellationToken);
        ApiPullRequestReview? changesRequested = reviews?.LastOrDefault(r => string.Equals(a: r.State, b: ChangesRequestedState, comparisonType: StringComparison.OrdinalIgnoreCase));

        if (changesRequested is null)
        {
            return (null, null, null, null);
        }

        return (changesRequested.State, TruncateBody(changesRequested.Body), changesRequested.User.Login, new Uri(changesRequested.HtmlUrl));
    }

    private async ValueTask<(string? name, Uri? url)> FetchFailedRunAsync(GitHubNotification notification, ApiPullRequest pr, CancellationToken cancellationToken)
    {
        if (!string.Equals(a: notification.Reason, b: CiActivityReason, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        string runsUrl = BuildWorkflowRunsUrl(repoFullName: notification.Repository.FullName, headSha: pr.Head.Sha);
        ApiWorkflowRunsResponse? runsResponse = await this.GetAsync<ApiWorkflowRunsResponse>(url: runsUrl, jsonTypeInfo: NotificationSerializerContext.Default.ApiWorkflowRunsResponse, cancellationToken: cancellationToken);
        ApiWorkflowRun? failedRun = runsResponse?.WorkflowRuns.FirstOrDefault(r => string.Equals(a: r.Conclusion, b: "failure", comparisonType: StringComparison.OrdinalIgnoreCase));

        if (failedRun is null)
        {
            return (null, null);
        }

        return (failedRun.Name, new Uri(failedRun.HtmlUrl));
    }

    private static string DetermineStatus(ApiPullRequest pr)
    {
        if (string.Equals(a: pr.State, b: "closed", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return "Closed";
        }

        return pr.Draft ? "Draft" : "Open";
    }

    private static string BuildCommentsUrl(string pullRequestApiUrl)
    {
        return pullRequestApiUrl.Replace(oldValue: "/pulls/", newValue: "/issues/", comparisonType: StringComparison.Ordinal) + "/comments?per_page=1&direction=desc";
    }

    private static string BuildReviewsUrl(string pullRequestApiUrl)
    {
        return pullRequestApiUrl + "/reviews?per_page=10";
    }

    private static string BuildWorkflowRunsUrl(string repoFullName, string headSha)
    {
        return $"repos/{repoFullName}/actions/runs?head_sha={headSha}&per_page=10";
    }

    private static string TruncateBody(string body)
    {
        return body.Length <= MaxBodyLength ? body : body[..MaxBodyLength] + "…";
    }

    private async ValueTask<T?> GetAsync<T>(string url, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
        where T : class
    {
        HttpClient httpClient = this._httpClientFactory.CreateClient("GitHub");

        using HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: url);
        using HttpResponseMessage response = await httpClient.SendAsync(request: request, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize(json: json, jsonTypeInfo: jsonTypeInfo);
    }
}
