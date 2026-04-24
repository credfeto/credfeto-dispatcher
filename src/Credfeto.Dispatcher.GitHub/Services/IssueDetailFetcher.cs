using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Helpers;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Models;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class IssueDetailFetcher : IIssueDetailFetcher
{
    private const string IssueType = "Issue";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubFilterOptions _filterOptions;

    public IssueDetailFetcher(IHttpClientFactory httpClientFactory, IOptions<GitHubOptions> options)
    {
        this._httpClientFactory = httpClientFactory;
        this._filterOptions = options.Value.Filter;
    }

    public async ValueTask<IssueDetails?> FetchAsync(GitHubNotification notification, CancellationToken cancellationToken)
    {
        if (!string.Equals(a: notification.Subject.Type, b: IssueType, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string apiUrl = notification.Subject.Url.ToString();
        ApiIssue? issue = await this.GetAsync<ApiIssue>(url: apiUrl, jsonTypeInfo: NotificationSerializerContext.Default.ApiIssue, cancellationToken: cancellationToken);

        if (issue is null)
        {
            return null;
        }

        IReadOnlyList<string> labels = [..issue.Labels.Select(l => l.Name)];
        WorkItemPriority priority = PriorityHelper.DeterminePriority(labels);
        bool onHold = OnHoldHelper.IsOnHold(labels, this._filterOptions.NoWorkFilter, this._filterOptions.LabelFilter);

        return new IssueDetails(
            Number: issue.Number,
            Title: issue.Title,
            Status: DetermineStatus(issue),
            Priority: priority,
            OnHold: onHold,
            HtmlUrl: new Uri(issue.HtmlUrl),
            Repository: ItemRepository.FromNotification(notification),
            LastNotification: LastNotification.FromNotification(notification)
        );
    }

    private static WorkItemStatus DetermineStatus(ApiIssue issue)
    {
        return string.Equals(a: issue.State, b: "closed", comparisonType: StringComparison.OrdinalIgnoreCase) ? WorkItemStatus.Closed : WorkItemStatus.Open;
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
