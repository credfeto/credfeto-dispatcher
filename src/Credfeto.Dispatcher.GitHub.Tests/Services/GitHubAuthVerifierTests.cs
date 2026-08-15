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
    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly IGitHubAuthVerifier _verifier;

    public GitHubAuthVerifierTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        this._verifier = new GitHubAuthVerifier(httpClientFactory: this._httpClientFactory);
    }

    private static (
        HttpClient Client,
        CapturingResponseHandler Handler
    ) MockHttpClientFactoryCreateClientWithCapturingHandler(
        System.Net.Http.IHttpClientFactory httpClientFactory,
        HttpStatusCode statusCode
    )
    {
        CapturingResponseHandler? handler = new(statusCode: statusCode);

        try
        {
            HttpClient client = new(handler: handler, disposeHandler: true)
            {
                BaseAddress = new Uri("https://api.github.com/"),
            };
            httpClientFactory.CreateClient("GitHub").Returns(client);
            (HttpClient Client, CapturingResponseHandler Handler) result = (client, handler);
            handler = null;

            return result;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    [Fact]
    public async Task VerifyAsyncCompletesWhenServerRespondsWithOkAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: HttpStatusCode.OK);

        await this._verifier.VerifyAsync(this.CancellationToken());
    }

    [Fact]
    public async Task VerifyAsyncRequestsTheUserEndpointAsync()
    {
        (HttpClient httpClient, CapturingResponseHandler handler) =
            MockHttpClientFactoryCreateClientWithCapturingHandler(
                httpClientFactory: this._httpClientFactory,
                statusCode: HttpStatusCode.OK
            );
        using (httpClient)
        {
            await this._verifier.VerifyAsync(this.CancellationToken());

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(
                expected: "https://api.github.com/user",
                actual: handler.CapturedRequest.RequestUri?.ToString()
            );
        }
    }

    [Fact]
    public async Task VerifyAsyncDoesNotSendIfNoneMatchHeaderAsync()
    {
        (HttpClient httpClient, CapturingResponseHandler handler) =
            MockHttpClientFactoryCreateClientWithCapturingHandler(
                httpClientFactory: this._httpClientFactory,
                statusCode: HttpStatusCode.OK
            );
        using (httpClient)
        {
            await this._verifier.VerifyAsync(this.CancellationToken());

            Assert.NotNull(handler.CapturedRequest);
            Assert.Empty(handler.CapturedRequest.Headers.IfNoneMatch);
        }
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
