using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Models;
using Credfeto.Dispatcher.GitHub.Services;
using Credfeto.Dispatcher.GitHub.Tests.Helpers;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class GitHubRepoHelperTests : TestBase
{
    private const string EMPTY_JSON = "[]";

    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;

    public GitHubRepoHelperTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
    }

    private GitHubRepoHelper CreateHelper()
    {
        return new(httpClientFactory: this._httpClientFactory, logger: this.GetTypedLogger<GitHubRepoHelper>());
    }

    [Fact]
    public async Task GetPagedAsync_WhenSuccessful_ReturnsNoFailureStatusAsync()
    {
        using HttpClient client = HttpClientTestFactory.Create(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

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
        using HttpClient client = HttpClientTestFactory.Create(statusCode);
        this._httpClientFactory.CreateClient("GitHub").Returns(client);

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
