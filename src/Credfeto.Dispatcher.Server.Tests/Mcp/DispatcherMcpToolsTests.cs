using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Server.Mcp;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.Server.Tests.Mcp;

public sealed class DispatcherMcpToolsTests : TestBase
{
    private const string INVALID_NAME = "not valid";

    private readonly DispatcherMcpTools _tools;
    private readonly IWorkItemRepository _workItemRepository;

    public DispatcherMcpToolsTests()
    {
        this._workItemRepository = GetSubstitute<IWorkItemRepository>();
        this._tools = new(workItemRepository: this._workItemRepository, options: Options.Create(new GitHubOptions()));
    }

    [Fact]
    public async Task GetPrioritiesPassesTheNormalisedUserToTheRepositoryAsync()
    {
        this._workItemRepository.GetPrioritisedWorkItemsAsync(
                owners: Arg.Any<IReadOnlyList<string>>(),
                maxIssues: Arg.Any<int>(),
                boostedRepos: Arg.Any<IReadOnlyList<string>>(),
                user: Arg.Any<string?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new PrioritiesResponse(Priorities: [], AsOf: DateTimeOffset.UnixEpoch, LagSeconds: 0));

        string json = await this._tools.GetPrioritiesAsync(
            user: " octocat ",
            cancellationToken: this.CancellationToken()
        );

        Assert.False(string.IsNullOrWhiteSpace(json), userMessage: "Expected JSON");
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
    public Task GetPrioritiesRejectsAnInvalidUserAsync()
    {
        return Assert.ThrowsAsync<McpException>(() =>
            this._tools.GetPrioritiesAsync(user: INVALID_NAME, cancellationToken: this.CancellationToken())
        );
    }

    [Fact]
    public async Task GetDatabaseStatePassesTheFiltersToTheRepositoryAsync()
    {
        this._workItemRepository.GetDatabaseStateAsync(
                owner: Arg.Any<string?>(),
                repo: Arg.Any<string?>(),
                user: Arg.Any<string?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new DbStateResponse(PullRequests: [], Issues: [], AsOf: DateTimeOffset.UnixEpoch));

        string json = await this._tools.GetDatabaseStateAsync(
            owner: "credfeto",
            repo: "credfeto-dispatcher",
            user: null,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(string.IsNullOrWhiteSpace(json), userMessage: "Expected JSON");
        await this
            ._workItemRepository.Received(1)
            .GetDatabaseStateAsync(
                owner: "credfeto",
                repo: "credfeto-dispatcher",
                user: null,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public Task GetDatabaseStateRejectsAnInvalidOwnerAsync()
    {
        return Assert.ThrowsAsync<McpException>(() =>
            this._tools.GetDatabaseStateAsync(
                owner: INVALID_NAME,
                repo: null,
                user: null,
                cancellationToken: this.CancellationToken()
            )
        );
    }

    [Fact]
    public Task GetDatabaseStateRejectsAnInvalidRepoAsync()
    {
        return Assert.ThrowsAsync<McpException>(() =>
            this._tools.GetDatabaseStateAsync(
                owner: null,
                repo: INVALID_NAME,
                user: null,
                cancellationToken: this.CancellationToken()
            )
        );
    }
}
