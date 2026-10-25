using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Models;
using Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class IssueDetailFetcher : IIssueDetailFetcher
{
    private const string ISSUE_TYPE = "Issue";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IssueDetailFetcher> _logger;

    public IssueDetailFetcher(IHttpClientFactory httpClientFactory, ILogger<IssueDetailFetcher> logger)
    {
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;
    }

    public async ValueTask<IssueDetails?> FetchAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    )
    {
        if (
            !string.Equals(
                a: notification.Subject.Type,
                b: ISSUE_TYPE,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return null;
        }

        if (notification.Subject.Url is null)
        {
            return null;
        }

        if (!TryGetIssueNumber(subjectUrl: notification.Subject.Url, out int issueNumber))
        {
            this._logger.LogIssueNumberNotFound(subjectUrl: notification.Subject.Url);

            return null;
        }

        ApiIssue? issue = await this.GetAsync<ApiIssue>(
            url: BuildIssueRelativeUrl(repoFullName: notification.Repository.FullName, issueNumber: issueNumber),
            jsonTypeInfo: NotificationSerializerContext.Default.ApiIssue,
            cancellationToken: cancellationToken
        );

        return issue is null ? null : ToIssueDetails(notification: notification, issue: issue);
    }

    private static IssueDetails? ToIssueDetails(GitHubNotification notification, ApiIssue issue)
    {
        IReadOnlyList<string> assignees = issue.Assignees?.Select(a => a.Login).ToList() ?? [];

        IReadOnlyList<string> labels = issue.Labels?.Select(l => l.Name).ToList() ?? [];

        if (issue.HtmlUrl is null)
        {
            return null;
        }

        Uri? linkedPullRequestUrl = issue.PullRequest is not null ? new Uri(issue.PullRequest.HtmlUrl) : null;

        string repoFullName = notification.Repository.FullName;
        string[] repoParts = repoFullName.Split('/');
        ItemRepository repository = new(Owner: repoParts[0], Name: repoParts[1], Url: notification.Repository.Url);
        LastNotification lastNotification = new(Id: notification.Id, Timestamp: notification.UpdatedAt);

        return new IssueDetails(
            Number: issue.Number,
            Title: issue.Title,
            Status: DetermineStatus(issue),
            HtmlUrl: new Uri(issue.HtmlUrl),
            Assignees: assignees,
            Labels: labels,
            LinkedPullRequestUrl: linkedPullRequestUrl,
            Repository: repository,
            LastNotification: lastNotification
        );
    }

    private static string DetermineStatus(ApiIssue issue)
    {
        return string.Equals(a: issue.State, b: "closed", comparisonType: StringComparison.OrdinalIgnoreCase)
            ? "Closed"
            : "Open";
    }

    // Subject.Url may be an api.github.com URL or a github.com HTML URL; requesting it verbatim bypasses
    // the proxy configured as the client's BaseAddress, so only the trailing issue number is taken from it.
    private static bool TryGetIssueNumber(Uri subjectUrl, out int issueNumber)
    {
        // Uri.Segments throws on a relative Uri, and deserialised URLs are not guaranteed to be absolute.
        if (!subjectUrl.IsAbsoluteUri)
        {
            issueNumber = 0;

            return false;
        }

        string lastSegment = subjectUrl.Segments.Length == 0 ? string.Empty : subjectUrl.Segments[^1].TrimEnd('/');

        return int.TryParse(
                s: lastSegment,
                style: NumberStyles.None,
                provider: CultureInfo.InvariantCulture,
                result: out issueNumber
            )
            && issueNumber > 0;
    }

    private static string BuildIssueRelativeUrl(string repoFullName, int issueNumber)
    {
        return string.Create(provider: CultureInfo.InvariantCulture, $"repos/{repoFullName}/issues/{issueNumber}");
    }

    internal static bool TryResolveOnBaseAddress(
        Uri? baseAddress,
        string relativeUrl,
        [NotNullWhen(true)] out Uri? requestUri
    )
    {
        if (
            baseAddress is not null
            && Uri.TryCreate(baseUri: baseAddress, relativeUri: relativeUrl, result: out Uri? resolved)
            && baseAddress.IsBaseOf(resolved)
        )
        {
            requestUri = resolved;

            return true;
        }

        requestUri = null;

        return false;
    }

    private async ValueTask<T?> GetAsync<T>(
        string url,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken
    )
        where T : class
    {
        HttpClient httpClient = this._httpClientFactory.CreateClient("GitHub");

        if (!TryResolveOnBaseAddress(baseAddress: httpClient.BaseAddress, relativeUrl: url, out Uri? requestUri))
        {
            this._logger.LogRequestNotOnApiBaseAddress(url: url, baseAddress: httpClient.BaseAddress);

            return null;
        }

        using HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: requestUri);
        using HttpResponseMessage response = await httpClient.SendAsync(
            request: request,
            cancellationToken: cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            this._logger.LogIssueFetchFailed(url: url, statusCode: response.StatusCode);

            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize(json: json, jsonTypeInfo: jsonTypeInfo);
    }
}
