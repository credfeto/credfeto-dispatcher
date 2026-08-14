using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Models;
using Credfeto.Dispatcher.GitHub.Services;
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
}
