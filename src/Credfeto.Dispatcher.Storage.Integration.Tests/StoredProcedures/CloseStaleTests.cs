using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.Database;
using Credfeto.Dispatcher.Storage.Database.Rows;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Integration.Tests.StoredProcedures;

public sealed class CloseStaleTests : SqlServerIntegrationTestBase
{
    [Fact]
    public async Task PullRequests_CloseStale_ClosesItemsNotInActiveListAsync()
    {
        CancellationToken ct = this.CancellationToken();
        await this.InsertOpenPullRequestAsync(id: 30, cancellationToken: ct);
        await this.InsertOpenPullRequestAsync(id: 31, cancellationToken: ct);

        await this.Database.ExecuteAsync(
            action: (c, innerCt) =>
                DispatcherDatabase.PullRequests_CloseStaleAsync(
                    connection: c,
                    repository: this.TestRepository,
                    activePrIds: "31",
                    cancellationToken: innerCt
                ),
            cancellationToken: ct
        );

        PullRequestRow remaining = Assert.Single(await this.GetActivePullRequestsForTestRepoAsync());
        Assert.Equal(expected: 31, actual: remaining.Id);
    }

    [Fact]
    public async Task PullRequests_CloseStale_WithNullActiveList_ClosesAllAsync()
    {
        CancellationToken ct = this.CancellationToken();
        await this.InsertOpenPullRequestAsync(id: 32, cancellationToken: ct);

        await this.Database.ExecuteAsync(
            action: (c, innerCt) =>
                DispatcherDatabase.PullRequests_CloseStaleAsync(
                    connection: c,
                    repository: this.TestRepository,
                    activePrIds: null,
                    cancellationToken: innerCt
                ),
            cancellationToken: ct
        );

        Assert.Empty(await this.GetActivePullRequestsForTestRepoAsync());
    }

    [Fact]
    public async Task Issues_CloseStale_ClosesItemsNotInActiveListAsync()
    {
        CancellationToken ct = this.CancellationToken();
        await this.InsertOpenIssueAsync(id: 40, cancellationToken: ct);
        await this.InsertOpenIssueAsync(id: 41, cancellationToken: ct);

        await this.Database.ExecuteAsync(
            action: (c, innerCt) =>
                DispatcherDatabase.Issues_CloseStaleAsync(
                    connection: c,
                    repository: this.TestRepository,
                    activeIssueIds: "41",
                    cancellationToken: innerCt
                ),
            cancellationToken: ct
        );

        IssueRow remaining = Assert.Single(await this.GetActiveIssuesForTestRepoAsync());
        Assert.Equal(expected: 41, actual: remaining.Id);
    }

    [Fact]
    public async Task Issues_CloseStale_WithNullActiveList_ClosesAllAsync()
    {
        CancellationToken ct = this.CancellationToken();
        await this.InsertOpenIssueAsync(id: 42, cancellationToken: ct);

        await this.Database.ExecuteAsync(
            action: (c, innerCt) =>
                DispatcherDatabase.Issues_CloseStaleAsync(
                    connection: c,
                    repository: this.TestRepository,
                    activeIssueIds: null,
                    cancellationToken: innerCt
                ),
            cancellationToken: ct
        );

        Assert.Empty(await this.GetActiveIssuesForTestRepoAsync());
    }
}
