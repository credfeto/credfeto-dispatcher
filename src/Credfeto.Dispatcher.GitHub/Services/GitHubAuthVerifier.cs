using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class GitHubAuthVerifier : IGitHubAuthVerifier
{
    private static readonly Uri UserRelativeUri = new(uriString: "user", uriKind: UriKind.Relative);

    private readonly IHttpClientFactory _httpClientFactory;

    public GitHubAuthVerifier(IHttpClientFactory httpClientFactory)
    {
        this._httpClientFactory = httpClientFactory;
    }

    public async ValueTask VerifyAsync(CancellationToken cancellationToken)
    {
        HttpClient httpClient = this._httpClientFactory.CreateClient("GitHub");

        using HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: UserRelativeUri);
        using HttpResponseMessage response = await httpClient.SendAsync(
            request: request,
            completionOption: HttpCompletionOption.ResponseHeadersRead,
            cancellationToken: cancellationToken
        );

        _ = response.EnsureSuccessStatusCode();
    }
}
