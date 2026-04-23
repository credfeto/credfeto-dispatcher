using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Helpers;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Models;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed partial class PullRequestDetailFetcher : IPullRequestDetailFetcher
{
    private const string PullRequestType = "PullRequest";
    private const int MaxBodyLength = 300;

    [GeneratedRegex(pattern: @"(?:closes?|fixes?|resolves?)\s+#(?<number>\d+)", options: RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinkedItemRegex();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubFilterOptions _filterOptions;

    public PullRequestDetailFetcher(IHttpClientFactory httpClientFactory, GitHubFilterOptions filterOptions)
    {
        this._httpClientFactory = httpClientFactory;
        this._filterOptions = filterOptions;
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

        IReadOnlySet<string> requiredContexts = await this.FetchRequiredStatusChecksAsync(
            repoFullName: notification.Repository.FullName,
            baseBranch: pr.Base.Ref,
            cancellationToken: cancellationToken);

        IReadOnlyList<PullRequestComment> comments = await this.FetchCommentsAsync(apiUrl: apiUrl, cancellationToken: cancellationToken);
        IReadOnlyList<PullRequestReview> reviews = await this.FetchReviewsAsync(apiUrl: apiUrl, cancellationToken: cancellationToken);
        IReadOnlyList<PullRequestRun> runs = await this.FetchRunsAsync(
            repoFullName: notification.Repository.FullName,
            headSha: pr.Head.Sha,
            requiredContexts: requiredContexts,
            cancellationToken: cancellationToken);

        IReadOnlyList<LinkedItem> linkedItems = ParseLinkedItems(pr.Body);
        IReadOnlyList<string> labels = [..pr.Labels.Select(l => l.Name)];
        string priority = PriorityHelper.DeterminePriority(labels);
        bool onHold = OnHoldHelper.IsOnHold(labels, this._filterOptions.NoWorkFilter);

        return new PullRequestDetails(
            Number: pr.Number,
            Title: pr.Title,
            Body: pr.Body is not null ? TruncateBody(pr.Body) : null,
            Status: DetermineStatus(pr),
            Priority: priority,
            OnHold: onHold,
            HtmlUrl: new Uri(pr.HtmlUrl),
            Repository: ItemRepository.FromNotification(notification),
            LastNotification: LastNotification.FromNotification(notification),
            Assignees: [..pr.Assignees.Select(u => u.Login)],
            Labels: labels,
            Comments: comments,
            Reviews: reviews,
            Runs: runs,
            LinkedItems: linkedItems
        );
    }

    private async ValueTask<IReadOnlySet<string>> FetchRequiredStatusChecksAsync(string repoFullName, string baseBranch, CancellationToken cancellationToken)
    {
        string url = $"repos/{repoFullName}/branches/{Uri.EscapeDataString(baseBranch)}/protection/required_status_checks";
        ApiRequiredStatusChecks? result = await this.GetAsync<ApiRequiredStatusChecks>(url: url, jsonTypeInfo: NotificationSerializerContext.Default.ApiRequiredStatusChecks, cancellationToken: cancellationToken);

        return result is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(result.Contexts, StringComparer.OrdinalIgnoreCase);
    }

    private async ValueTask<IReadOnlyList<PullRequestComment>> FetchCommentsAsync(string apiUrl, CancellationToken cancellationToken)
    {
        string commentsUrl = BuildCommentsUrl(apiUrl);
        ApiIssueComment[]? comments = await this.GetAsync<ApiIssueComment[]>(url: commentsUrl, jsonTypeInfo: NotificationSerializerContext.Default.ApiIssueCommentArray, cancellationToken: cancellationToken);

        if (comments is null)
        {
            return [];
        }

        return [..comments.Select(c => new PullRequestComment(
            Author: c.User.Login,
            Body: TruncateBody(c.Body),
            Url: new Uri(c.HtmlUrl),
            CreatedAt: c.CreatedAt))];
    }

    private async ValueTask<IReadOnlyList<PullRequestReview>> FetchReviewsAsync(string apiUrl, CancellationToken cancellationToken)
    {
        string reviewsUrl = BuildReviewsUrl(apiUrl);
        ApiPullRequestReview[]? reviews = await this.GetAsync<ApiPullRequestReview[]>(url: reviewsUrl, jsonTypeInfo: NotificationSerializerContext.Default.ApiPullRequestReviewArray, cancellationToken: cancellationToken);

        if (reviews is null)
        {
            return [];
        }

        return [..reviews.Select(r => new PullRequestReview(
            Author: r.User.Login,
            State: r.State,
            Body: string.IsNullOrWhiteSpace(r.Body) ? null : TruncateBody(r.Body),
            Url: new Uri(r.HtmlUrl),
            SubmittedAt: r.SubmittedAt))];
    }

    private async ValueTask<IReadOnlyList<PullRequestRun>> FetchRunsAsync(string repoFullName, string headSha, IReadOnlySet<string> requiredContexts, CancellationToken cancellationToken)
    {
        string runsUrl = BuildWorkflowRunsUrl(repoFullName: repoFullName, headSha: headSha);
        ApiWorkflowRunsResponse? runsResponse = await this.GetAsync<ApiWorkflowRunsResponse>(url: runsUrl, jsonTypeInfo: NotificationSerializerContext.Default.ApiWorkflowRunsResponse, cancellationToken: cancellationToken);

        if (runsResponse is null)
        {
            return [];
        }

        return [..runsResponse.WorkflowRuns.Select(r => new PullRequestRun(
            Name: r.Name,
            Status: r.Status,
            Conclusion: r.Conclusion,
            Url: new Uri(r.HtmlUrl),
            IsRequired: requiredContexts.Contains(r.Name)))];
    }

    private static IReadOnlyList<LinkedItem> ParseLinkedItems(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        IEnumerable<LinkedItem> items = LinkedItemRegex().Matches(body)
            .Select(m => new LinkedItem(
                Number: int.Parse(m.Groups["number"].Value, CultureInfo.InvariantCulture),
                Title: string.Empty,
                State: string.Empty,
                Url: new Uri($"https://github.com/issues/{m.Groups["number"].Value}")));

        return [..items.DistinctBy(i => i.Number)];
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
        return pullRequestApiUrl.Replace(oldValue: "/pulls/", newValue: "/issues/", comparisonType: StringComparison.Ordinal) + "/comments?per_page=100";
    }

    private static string BuildReviewsUrl(string pullRequestApiUrl)
    {
        return pullRequestApiUrl + "/reviews?per_page=100";
    }

    private static string BuildWorkflowRunsUrl(string repoFullName, string headSha)
    {
        return $"repos/{repoFullName}/actions/runs?head_sha={headSha}&per_page=100";
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
