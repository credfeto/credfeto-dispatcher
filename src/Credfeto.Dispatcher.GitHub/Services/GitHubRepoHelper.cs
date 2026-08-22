using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Models;
using Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class GitHubRepoHelper
{
    private const string USER_REPOS_URL = "user/repos?affiliation=owner,collaborator,organization_member&per_page=100";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubRepoHelper> _logger;

    public GitHubRepoHelper(IHttpClientFactory httpClientFactory, ILogger<GitHubRepoHelper> logger)
    {
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;
    }

    internal async Task<(
        bool DiscoveryComplete,
        IReadOnlyList<string> Active,
        IReadOnlyList<string> Inactive
    )> DiscoverReposAsync(Func<ApiUserRepo, bool> shouldInclude, CancellationToken cancellationToken)
    {
        List<string> active = [];
        List<string> inactive = [];
        string? url = USER_REPOS_URL;

        while (url is not null)
        {
            (ApiUserRepo[]? items, string? nextUrl, _) = await this.GetPagedAsync(
                url: url,
                jsonTypeInfo: NotificationSerializerContext.Default.ApiUserRepoArray,
                cancellationToken: cancellationToken
            );

            if (items is null)
            {
                return (false, active, inactive);
            }

            foreach (ApiUserRepo repo in items)
            {
                if (shouldInclude(repo))
                {
                    active.Add(repo.FullName);
                }
                else if (repo.Archived || repo.Disabled)
                {
                    inactive.Add(repo.FullName);
                }
            }

            url = nextUrl;
        }

        return (true, active, inactive);
    }

    internal async ValueTask<(T[]? items, string? nextUrl, HttpStatusCode? failureStatus)> GetPagedAsync<T>(
        string url,
        JsonTypeInfo<T[]> jsonTypeInfo,
        CancellationToken cancellationToken
    )
        where T : class
    {
        PagedETagResult<T> result = await this.GetPagedWithETagAsync(
            url: url,
            jsonTypeInfo: jsonTypeInfo,
            eTag: null,
            cancellationToken: cancellationToken
        );

        return (result.Items, result.NextUrl, result.FailureStatus);
    }

    internal async ValueTask<PagedETagResult<T>> GetPagedWithETagAsync<T>(
        string url,
        JsonTypeInfo<T[]> jsonTypeInfo,
        string? eTag,
        CancellationToken cancellationToken
    )
        where T : class
    {
        HttpClient httpClient = this._httpClientFactory.CreateClient("GitHub");

        using HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: url);

        ETagHeaderUtility.ApplyIfNoneMatch(request: request, eTag: eTag);

        using HttpResponseMessage response = await httpClient.SendAsync(
            request: request,
            cancellationToken: cancellationToken
        );

        int? pollIntervalSeconds = ETagHeaderUtility.ExtractPollIntervalSeconds(response.Headers);
        string? responseETag = ETagHeaderUtility.ExtractETag(response.Headers);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new(
                Items: null,
                NextUrl: null,
                FailureStatus: null,
                ETag: responseETag,
                NotModified: true,
                PollIntervalSeconds: pollIntervalSeconds
            );
        }

        if (!response.IsSuccessStatusCode)
        {
            this._logger.LogPageFetchFailed(url: url);

            return new(
                Items: null,
                NextUrl: null,
                FailureStatus: response.StatusCode,
                ETag: null,
                NotModified: false,
                PollIntervalSeconds: pollIntervalSeconds
            );
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        T[]? items = JsonSerializer.Deserialize(json: json, jsonTypeInfo: jsonTypeInfo);
        string? nextUrl = ParseNextLink(response.Headers);

        return new(
            Items: items,
            NextUrl: nextUrl,
            FailureStatus: null,
            ETag: responseETag,
            NotModified: false,
            PollIntervalSeconds: pollIntervalSeconds
        );
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

                if (sections[1].Trim().Equals(value: "rel=\"next\"", comparisonType: StringComparison.Ordinal))
                {
                    return ToRelativeUrl(sections[0].Trim().Trim('<', '>'));
                }
            }
        }

        return null;
    }

    // GitHub's Link header always advertises the real api.github.com host, even when the request was
    // served through a proxy (ApiBaseUrl pointed at github-api.markridgwell.com). Following that
    // absolute URL verbatim would send the next page request straight to api.github.com, bypassing the
    // configured base address and the proxy - and re-using the client's Authorization header, which is
    // only valid against the proxy. Strip the URL down to its path and query so it is always resolved
    // relative to the "GitHub" HttpClient's own BaseAddress instead.
    private static string ToRelativeUrl(string url)
    {
        return Uri.TryCreate(uriString: url, uriKind: UriKind.Absolute, out Uri? absolute)
            ? absolute.PathAndQuery
            : url;
    }
}
