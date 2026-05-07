using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Models;
using Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class WorkItemScanner : IWorkItemScanner
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WorkItemScanner> _logger;
    private readonly INotificationStateTracker _notificationStateTracker;
    private readonly GitHubOptions _options;

    public WorkItemScanner(
        IHttpClientFactory httpClientFactory,
        INotificationStateTracker notificationStateTracker,
        IOptions<GitHubOptions> options,
        ILogger<WorkItemScanner> logger
    )
    {
        this._httpClientFactory = httpClientFactory;
        this._notificationStateTracker = notificationStateTracker;
        this._options = options.Value;
        this._logger = logger;
    }

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> repos = await this.DiscoverReposAsync(cancellationToken);

        if (repos.Count == 0)
        {
            this._logger.LogNoReposDiscovered();

            return;
        }

        this._logger.LogDiscoveredRepos(count: repos.Count);

        foreach (string repo in repos)
        {
            await this.ScanRepoAsync(repo: repo, cancellationToken: cancellationToken);
        }

        this._logger.LogScanComplete();
    }

    private async Task<IReadOnlyList<string>> DiscoverReposAsync(
        CancellationToken cancellationToken
    )
    {
        List<string> repos = [];
        string? url = "user/repos?per_page=100";

        while (url is not null)
        {
            (ApiUserRepo[]? items, string? nextUrl) = await this.GetPagedAsync(
                url: url,
                jsonTypeInfo: NotificationSerializerContext.Default.ApiUserRepoArray,
                cancellationToken: cancellationToken
            );

            if (items is null)
            {
                break;
            }

            foreach (ApiUserRepo repo in items)
            {
                if (
                    !repo.Archived
                    && !repo.Disabled
                    && repo.Permissions?.Push == true
                    && this.PassesRepoFilter(repo.FullName)
                )
                {
                    repos.Add(repo.FullName);
                }
            }

            url = nextUrl;
        }

        return repos;
    }

    private bool PassesRepoFilter(string fullName)
    {
        if (this._options.Filter.AllowedOwners.Count > 0)
        {
            string owner = GetOwner(fullName);

            if (
                !this._options.Filter.AllowedOwners.Any(o =>
                    string.Equals(
                        a: o,
                        b: owner,
                        comparisonType: StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                return false;
            }
        }

        if (this._options.Filter.AllowedRepos.Count > 0)
        {
            if (
                !this._options.Filter.AllowedRepos.Any(r =>
                    string.Equals(
                        a: r,
                        b: fullName,
                        comparisonType: StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                return false;
            }
        }

        if (this._options.Filter.ExcludedRepos.Count > 0)
        {
            if (
                this._options.Filter.ExcludedRepos.Any(r =>
                    string.Equals(
                        a: r,
                        b: fullName,
                        comparisonType: StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                return false;
            }
        }

        return true;
    }

    private static string GetOwner(string fullName)
    {
        int slash = fullName.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);

        return slash >= 0 ? fullName[..slash] : fullName;
    }

    private async Task ScanRepoAsync(string repo, CancellationToken cancellationToken)
    {
        this._logger.LogScanningRepo(repo: repo);

        await this.ScanPullRequestsAsync(repo: repo, cancellationToken: cancellationToken);
        await this.ScanIssuesAsync(repo: repo, cancellationToken: cancellationToken);
    }

    private async Task ScanPullRequestsAsync(string repo, CancellationToken cancellationToken)
    {
        string? url = $"repos/{repo}/pulls?state=open&per_page=100";

        while (url is not null)
        {
            (ApiPullRequest[]? items, string? nextUrl) = await this.GetPagedAsync(
                url: url,
                jsonTypeInfo: NotificationSerializerContext.Default.ApiPullRequestArray,
                cancellationToken: cancellationToken
            );

            if (items is null)
            {
                break;
            }

            foreach (ApiPullRequest pr in items)
            {
                IReadOnlyList<string> labelNames = [.. pr.Labels.Select(l => l.Name)];

                if (!this.PassesLabelFilter(labelNames))
                {
                    continue;
                }

                WorkPriority priority = LabelParser.ParsePriority(labelNames);
                bool isOnHold = LabelParser.IsOnHold(
                    labels: labelNames,
                    noWorkFilter: this._options.Filter.NoWorkFilter
                );
                string status = pr.Draft ? "Draft" : "Open";

                await this._notificationStateTracker.UpdatePullRequestStateAsync(
                    repository: repo,
                    pullRequestNumber: pr.Number,
                    status: status,
                    priority: priority,
                    isOnHold: isOnHold,
                    cancellationToken: cancellationToken
                );

                this._logger.LogScannedPullRequest(repo: repo, number: pr.Number, status: status);
            }

            url = nextUrl;
        }
    }

    private async Task ScanIssuesAsync(string repo, CancellationToken cancellationToken)
    {
        string? url = $"repos/{repo}/issues?state=open&per_page=100";

        while (url is not null)
        {
            (ApiIssue[]? items, string? nextUrl) = await this.GetPagedAsync(
                url: url,
                jsonTypeInfo: NotificationSerializerContext.Default.ApiIssueArray,
                cancellationToken: cancellationToken
            );

            if (items is null)
            {
                break;
            }

            foreach (ApiIssue issue in items)
            {
                if (issue.PullRequest is not null)
                {
                    continue;
                }

                IReadOnlyList<string> labelNames = issue.Labels is null
                    ? []
                    : [.. issue.Labels.Select(l => l.Name)];

                if (!this.PassesLabelFilter(labelNames))
                {
                    continue;
                }

                WorkPriority priority = LabelParser.ParsePriority(labelNames);
                bool isOnHold = LabelParser.IsOnHold(
                    labels: labelNames,
                    noWorkFilter: this._options.Filter.NoWorkFilter
                );

                await this._notificationStateTracker.UpdateIssueStateAsync(
                    repository: repo,
                    issueNumber: issue.Number,
                    status: "Open",
                    priority: priority,
                    isOnHold: isOnHold,
                    hasLinkedPr: false,
                    cancellationToken: cancellationToken
                );

                this._logger.LogScannedIssue(repo: repo, number: issue.Number);
            }

            url = nextUrl;
        }
    }

    private bool PassesLabelFilter(IReadOnlyList<string> labelNames)
    {
        if (this._options.Filter.LabelFilter.Count == 0)
        {
            return true;
        }

        return labelNames.Any(label =>
            this._options.Filter.LabelFilter.Any(filter =>
                string.Equals(
                    a: label,
                    b: filter,
                    comparisonType: StringComparison.OrdinalIgnoreCase
                )
            )
        );
    }

    private async ValueTask<(T[]? items, string? nextUrl)> GetPagedAsync<T>(
        string url,
        JsonTypeInfo<T[]> jsonTypeInfo,
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
            this._logger.LogPageFetchFailed(url: url);

            return (null, null);
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        T[]? items = JsonSerializer.Deserialize(json: json, jsonTypeInfo: jsonTypeInfo);
        string? nextUrl = ParseNextLink(response.Headers);

        return (items, nextUrl);
    }

    private static string? ParseNextLink(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues(name: "Link", out IEnumerable<string>? linkValues))
        {
            return null;
        }

        foreach (string linkHeader in linkValues)
        {
            foreach (string part in linkHeader.Split(','))
            {
                string[] sections = part.Split(';');

                if (sections.Length != 2)
                {
                    continue;
                }

                if (
                    sections[1]
                        .Trim()
                        .Equals(value: "rel=\"next\"", comparisonType: StringComparison.Ordinal)
                )
                {
                    return sections[0].Trim().Trim('<', '>');
                }
            }
        }

        return null;
    }
}
