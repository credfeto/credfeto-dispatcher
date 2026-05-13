using System;
using System.Collections.Generic;
using System.Linq;
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
    private const string UserReposUrl = "user/repos?affiliation=owner,collaborator,organization_member&per_page=100";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubRepoHelper> _logger;

    public GitHubRepoHelper(IHttpClientFactory httpClientFactory, ILogger<GitHubRepoHelper> logger)
    {
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;
    }

    internal async Task<IReadOnlyList<string>> DiscoverReposAsync(
        Func<ApiUserRepo, bool> shouldInclude,
        CancellationToken cancellationToken
    )
    {
        List<string> repos = [];
        string? url = UserReposUrl;

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

            repos.AddRange(items.Where(shouldInclude).Select(r => r.FullName));

            url = nextUrl;
        }

        return repos;
    }

    internal async ValueTask<(T[]? items, string? nextUrl)> GetPagedAsync<T>(
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

                if (sections[1].Trim().Equals(value: "rel=\"next\"", comparisonType: StringComparison.Ordinal))
                {
                    return sections[0].Trim().Trim('<', '>');
                }
            }
        }

        return null;
    }
}
