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

public sealed class IssueDetailFetcher : IIssueDetailFetcher
{
    private const string IssueType = "Issue";

    private readonly IHttpClientFactory _httpClientFactory;

    public IssueDetailFetcher(IHttpClientFactory httpClientFactory)
    {
        this._httpClientFactory = httpClientFactory;
    }

    public async ValueTask<IssueDetails?> FetchAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    )
    {
        if (
            !string.Equals(
                a: notification.Subject.Type,
                b: IssueType,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return null;
        }

        string apiUrl = notification.Subject.Url.ToString();
        ApiIssue? issue = await this.GetAsync<ApiIssue>(
            url: apiUrl,
            jsonTypeInfo: NotificationSerializerContext.Default.ApiIssue,
            cancellationToken: cancellationToken
        );

        if (issue is null)
        {
            return null;
        }

        IReadOnlyList<string> assignees = issue.Assignees?.Select(a => a.Login).ToList() ?? [];

        IReadOnlyList<string> labels = issue.Labels?.Select(l => l.Name).ToList() ?? [];

        Uri? linkedPullRequestUrl = issue.PullRequest is not null
            ? new Uri(issue.PullRequest.HtmlUrl)
            : null;

        return new IssueDetails(
            Number: issue.Number,
            Title: issue.Title,
            Status: DetermineStatus(issue),
            HtmlUrl: new Uri(issue.HtmlUrl),
            Assignees: assignees,
            Labels: labels,
            LinkedPullRequestUrl: linkedPullRequestUrl
        );
    }

    private static string DetermineStatus(ApiIssue issue)
    {
        return string.Equals(
            a: issue.State,
            b: "closed",
            comparisonType: StringComparison.OrdinalIgnoreCase
        )
            ? "Closed"
            : "Open";
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
