using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.Database;
using Credfeto.Dispatcher.Storage.Database.Rows;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Integration.Tests.StoredProcedures;

public sealed class PullRequestsGetActiveTests : SqlServerIntegrationTestBase
{
    private ValueTask InsertPullRequestAsync(
        int id,
        string status = "Open",
        int priority = 2,
        bool isOnHold = false,
        in CancellationToken cancellationToken = default
    ) =>
        this.Database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.PullRequests_UpsertAsync(
                    connection: c,
                    repository: this.TestRepository,
                    id: id,
                    status: status,
                    priority: priority,
                    isOnHold: isOnHold,
                    commentCount: 0,
                    reviewDecision: null,
                    failedCheckCount: 0,
                    failedCheckNames: null,
                    failedCheckSha: null,
                    author: null,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );

    [Fact]
    public async Task OpenPullRequest_IsReturnedAsync()
    {
        await this.InsertPullRequestAsync(id: 1, cancellationToken: this.CancellationToken());

        IReadOnlyList<PullRequestRow> rows = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.PullRequests_GetActiveAsync,
            cancellationToken: this.CancellationToken()
        );

        PullRequestRow row = Assert.Single(this.ForTestRepo(rows));
        Assert.Equal(expected: 1, actual: row.Id);
        Assert.Equal(expected: "Open", actual: row.Status);
    }

    [Fact]
    public async Task DraftPullRequest_IsReturnedAsync()
    {
        await this.InsertPullRequestAsync(id: 2, status: "Draft", cancellationToken: this.CancellationToken());

        IReadOnlyList<PullRequestRow> rows = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.PullRequests_GetActiveAsync,
            cancellationToken: this.CancellationToken()
        );

        PullRequestRow row = Assert.Single(this.ForTestRepo(rows));
        Assert.Equal(expected: 2, actual: row.Id);
        Assert.Equal(expected: "Draft", actual: row.Status);
    }

    [Fact]
    public async Task ClosedPullRequest_IsExcludedAsync()
    {
        await this.InsertPullRequestAsync(id: 3, status: "Closed", cancellationToken: this.CancellationToken());

        IReadOnlyList<PullRequestRow> rows = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.PullRequests_GetActiveAsync,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(this.ForTestRepo(rows));
    }

    [Fact]
    public async Task OnHoldPullRequest_IsExcludedAsync()
    {
        await this.InsertPullRequestAsync(id: 4, isOnHold: true, cancellationToken: this.CancellationToken());

        IReadOnlyList<PullRequestRow> rows = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.PullRequests_GetActiveAsync,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(this.ForTestRepo(rows));
    }

    [Fact]
    public async Task PullRequestForInactiveRepo_IsExcludedAsync()
    {
        string otherRepo = $"test-{Guid.NewGuid():N}/other";
        CancellationToken ct = this.CancellationToken();

        await this.Database.ExecuteAsync(
            action: (c, innerCt) =>
                DispatcherDatabase.Repos_SetActiveAsync(
                    connection: c,
                    repositories: this.TestRepository,
                    cancellationToken: innerCt
                ),
            cancellationToken: ct
        );

        await this.InsertPullRequestAsync(id: 5, cancellationToken: ct);

        await this.Database.ExecuteAsync(
            action: (c, innerCt) =>
                DispatcherDatabase.Repos_SetActiveAsync(
                    connection: c,
                    repositories: otherRepo,
                    cancellationToken: innerCt
                ),
            cancellationToken: ct
        );

        IReadOnlyList<PullRequestRow> rows = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.PullRequests_GetActiveAsync,
            cancellationToken: ct
        );

        Assert.Empty(this.ForTestRepo(rows));
    }
}
