using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using Credfeto.Dispatcher.GitHub.Tests.Helpers;
using FunFair.Test.Common;
using FunFair.Test.Common.Extensions;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class GitHubAuthVerifierTests : TestBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGitHubAuthVerifier _verifier;

    public GitHubAuthVerifierTests()
    {
        this._httpClientFactory = GetSubstitute<IHttpClientFactory>();
        this._verifier = new GitHubAuthVerifier(httpClientFactory: this._httpClientFactory);
    }

    [Fact]
    public async Task VerifyAsyncCompletesWhenServerRespondsWithOkAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK);

        await this._verifier.VerifyAsync(this.CancellationToken());
    }

    [Fact]
    public async Task VerifyAsyncRequestsTheUserEndpointWithoutIfNoneMatchHeaderAsync()
    {
        using CapturingResponseHandler handler = new(statusCode: HttpStatusCode.OK);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.github.com/") };
        this._httpClientFactory.CreateClient("GitHub").Returns(httpClient);

        await this._verifier.VerifyAsync(this.CancellationToken());

        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal(expected: "https://api.github.com/user", actual: handler.CapturedRequest.RequestUri?.ToString());
        Assert.Empty(handler.CapturedRequest.Headers.IfNoneMatch);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task VerifyAsyncThrowsHttpRequestExceptionWithStatusCodeOnFailureAsync(HttpStatusCode statusCode)
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: statusCode);

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            this._verifier.VerifyAsync(this.CancellationToken()).AsTask()
        );

        Assert.Equal(expected: statusCode, actual: exception.StatusCode);
    }
}
