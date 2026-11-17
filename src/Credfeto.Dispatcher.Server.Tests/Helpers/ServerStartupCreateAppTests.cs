using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Server.Helpers;
using FunFair.Test.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.Server.Tests.Helpers;

[Collection(ServerEnvironment.NAME)]
public sealed class ServerStartupCreateAppTests : TestBase
{
    private const string PRIORITIES_PATTERN = "/priorities";

    private readonly IWorkItemRepository _workItemRepository;

    public ServerStartupCreateAppTests()
    {
        this._workItemRepository = GetSubstitute<IWorkItemRepository>();
    }

    [Fact]
    public async Task CreateAppConfiguresKestrelAsync()
    {
        await using WebApplication app = ServerStartup.CreateApp([]);

        KestrelServerOptions options = app.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        Assert.False(options.AddServerHeader, userMessage: "Server header should be disabled");
        Assert.False(options.AllowSynchronousIO, userMessage: "Synchronous IO should be disabled");
    }

    [Fact]
    public async Task RequestPipelineAddsVersionHeaderAsync()
    {
        await using WebApplication app = ServerStartup.CreateApp([]);
        RequestDelegate pipeline = ((IApplicationBuilder)app).Build();
        await using MemoryStream body = new();
        DefaultHttpContext context = CreateContext(services: app.Services, body: body, queryString: QueryString.Empty);

        await pipeline(context);

        Assert.True(context.Response.Headers.ContainsKey("X-Version"), userMessage: "Expected X-Version header");
    }

    [Fact]
    public async Task PingEndpointReturnsOkAsync()
    {
        await using WebApplication app = ServerStartup.CreateApp([]);
        await using MemoryStream body = new();
        DefaultHttpContext context = CreateContext(services: app.Services, body: body, queryString: QueryString.Empty);

        await InvokeEndpointAsync(app: app, pattern: "/ping", context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.True(body.Length > 0, userMessage: "Expected a response body");
    }

    [Fact]
    public async Task PrioritiesEndpointRejectsAnInvalidUserAsync()
    {
        int statusCode = await this.InvokePrioritiesAsync(new QueryString("?user=not%20valid"));

        Assert.Equal(expected: StatusCodes.Status400BadRequest, actual: statusCode);
        await this.DidNotReceiveGetPrioritisedWorkItemsAsync();
    }

    [Fact]
    public async Task PrioritiesEndpointReturnsTheRepositoryResponseAsync()
    {
        this.MockGetPrioritisedWorkItems(
            Task.FromResult(new PrioritiesResponse(Priorities: [], AsOf: DateTimeOffset.UnixEpoch, LagSeconds: 0))
        );

        int statusCode = await this.InvokePrioritiesAsync(new QueryString("?user=%20octocat%20"));

        Assert.Equal(expected: StatusCodes.Status200OK, actual: statusCode);
        await this
            ._workItemRepository.Received(1)
            .GetPrioritisedWorkItemsAsync(
                owners: Arg.Any<IReadOnlyList<string>>(),
                maxIssues: Arg.Any<int>(),
                boostedRepos: Arg.Any<IReadOnlyList<string>>(),
                user: "octocat",
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PrioritiesEndpointReturnsAProblemWhenTheRepositoryFailsAsync()
    {
        this.MockGetPrioritisedWorkItems(
            Task.FromException<PrioritiesResponse>(new InvalidOperationException("Repository failed"))
        );

        int statusCode = await this.InvokePrioritiesAsync(QueryString.Empty);

        Assert.Equal(expected: StatusCodes.Status500InternalServerError, actual: statusCode);
    }

    [Fact]
    public Task PrioritiesEndpointRethrowsCancellationAsync()
    {
        this.MockGetPrioritisedWorkItems(Task.FromException<PrioritiesResponse>(new OperationCanceledException()));

        return Assert.ThrowsAsync<OperationCanceledException>(() => this.InvokePrioritiesAsync(QueryString.Empty));
    }

    private void MockGetPrioritisedWorkItems(Task<PrioritiesResponse> result)
    {
        this._workItemRepository.GetPrioritisedWorkItemsAsync(
                owners: Arg.Any<IReadOnlyList<string>>(),
                maxIssues: Arg.Any<int>(),
                boostedRepos: Arg.Any<IReadOnlyList<string>>(),
                user: Arg.Any<string?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(result);
    }

    private Task<PrioritiesResponse> DidNotReceiveGetPrioritisedWorkItemsAsync()
    {
        return this
            ._workItemRepository.DidNotReceive()
            .GetPrioritisedWorkItemsAsync(
                owners: Arg.Any<IReadOnlyList<string>>(),
                maxIssues: Arg.Any<int>(),
                boostedRepos: Arg.Any<IReadOnlyList<string>>(),
                user: Arg.Any<string?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    private async Task<int> InvokePrioritiesAsync(QueryString queryString)
    {
        await using WebApplication app = ServerStartup.CreateApp([]);
        await using ServiceProvider requestServices = new ServiceCollection()
            .AddLogging()
            .AddSingleton(this._workItemRepository)
            .AddSingleton(Options.Create(new GitHubOptions()))
            .BuildServiceProvider();
        await using MemoryStream body = new();
        DefaultHttpContext context = CreateContext(services: requestServices, body: body, queryString: queryString);

        await InvokeEndpointAsync(app: app, pattern: PRIORITIES_PATTERN, context: context);

        return context.Response.StatusCode;
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services, Stream body, in QueryString queryString)
    {
        DefaultHttpContext context = new() { RequestServices = services };
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = queryString;
        context.Response.Body = body;

        return context;
    }

    private static Task InvokeEndpointAsync(WebApplication app, string pattern, HttpContext context)
    {
        RouteEndpoint endpoint = ((IEndpointRouteBuilder)app)
            .DataSources.SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => StringComparer.Ordinal.Equals(x: e.RoutePattern.RawText, y: pattern));

        RequestDelegate requestDelegate = endpoint.RequestDelegate ?? throw new InvalidOperationException(pattern);

        return requestDelegate(context);
    }
}
