using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Models;
using Credfeto.Dispatcher.GitHub.Services;
using Credfeto.Dispatcher.GitHub.Tests.Helpers;
using FunFair.Test.Common;
using FunFair.Test.Common.Extensions;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class GitHubRepoHelperTests : TestBase
{
    private const string EMPTY_JSON = "[]";

    private readonly IHttpClientFactory _httpClientFactory;

    public GitHubRepoHelperTests()
    {
        this._httpClientFactory = GetSubstitute<IHttpClientFactory>();
    }

    private GitHubRepoHelper CreateHelper()
    {
        return new(httpClientFactory: this._httpClientFactory, logger: this.GetTypedLogger<GitHubRepoHelper>());
    }

    [Fact]
    public async Task GetPagedAsync_WhenSuccessful_ReturnsNoFailureStatusAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(
            clientName: "GitHub",
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: EMPTY_JSON
        );

        GitHubRepoHelper helper = this.CreateHelper();

        (ApiUserRepo[]? items, string? nextUrl, HttpStatusCode? failureStatus) = await helper.GetPagedAsync(
            url: "user/repos",
            jsonTypeInfo: NotificationSerializerContext.Default.ApiUserRepoArray,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(items);
        Assert.Null(nextUrl);
        Assert.Null(failureStatus);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetPagedAsync_WhenRequestFails_ReturnsMatchingFailureStatusAsync(HttpStatusCode statusCode)
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "GitHub", httpStatusCode: statusCode);

        GitHubRepoHelper helper = this.CreateHelper();

        (ApiUserRepo[]? items, string? nextUrl, HttpStatusCode? failureStatus) = await helper.GetPagedAsync(
            url: "user/repos",
            jsonTypeInfo: NotificationSerializerContext.Default.ApiUserRepoArray,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(items);
        Assert.Null(nextUrl);
        Assert.Equal(expected: statusCode, actual: failureStatus);
    }

    [Fact]
    public async Task GetPagedWithETagAsync_WhenStoredETagProvided_SendsIfNoneMatchHeaderAsync()
    {
        (HttpClient client, FixedResponseHandler handler) = HttpClientTestFactory.CreateWithHandler(
            statusCode: HttpStatusCode.OK,
            content: EMPTY_JSON
        );
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubRepoHelper helper = this.CreateHelper();

        _ = await helper.GetPagedWithETagAsync(
            url: "repos/owner/repo/events",
            jsonTypeInfo: NotificationSerializerContext.Default.ApiUserRepoArray,
            eTag: "\"stored-etag\"",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: "\"stored-etag\"", actual: handler.LastRequestIfNoneMatch);
    }

    [Fact]
    public async Task GetPagedWithETagAsync_WhenNotModified_ReturnsNotModifiedWithNoItemsAsync()
    {
        this._httpClientFactory.CreateClient("GitHub")
            .Returns(HttpClientTestFactory.Create(HttpStatusCode.NotModified));

        GitHubRepoHelper helper = this.CreateHelper();

        PagedETagResult<ApiUserRepo> result = await helper.GetPagedWithETagAsync(
            url: "repos/owner/repo/events",
            jsonTypeInfo: NotificationSerializerContext.Default.ApiUserRepoArray,
            eTag: "\"stored-etag\"",
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(result.Items);
        Assert.Null(result.NextUrl);
        Assert.Null(result.ETag);
        Assert.True(result.NotModified, userMessage: "Expected response to be reported as not modified");
        Assert.Null(result.PollIntervalSeconds);
    }

    [Fact]
    public async Task GetPagedWithETagAsync_WhenSuccessful_ReturnsResponseETagAndPollIntervalAsync()
    {
        (HttpClient client, FixedResponseHandler _) = HttpClientTestFactory.CreateWithHandler(
            statusCode: HttpStatusCode.OK,
            content: EMPTY_JSON,
            eTag: "\"new-etag\"",
            pollIntervalSeconds: 90
        );
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

        GitHubRepoHelper helper = this.CreateHelper();

        PagedETagResult<ApiUserRepo> result = await helper.GetPagedWithETagAsync(
            url: "repos/owner/repo/events",
            jsonTypeInfo: NotificationSerializerContext.Default.ApiUserRepoArray,
            eTag: null,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result.Items);
        Assert.Null(result.NextUrl);
        Assert.Equal(expected: "\"new-etag\"", actual: result.ETag);
        Assert.False(result.NotModified, userMessage: "Expected response to not be reported as not modified");
        Assert.Equal(expected: 90, actual: result.PollIntervalSeconds);
    }
}
