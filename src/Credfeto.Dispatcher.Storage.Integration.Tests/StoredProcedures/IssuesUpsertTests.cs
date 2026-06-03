using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.Database;
using Credfeto.Dispatcher.Storage.Database.Rows;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Integration.Tests.StoredProcedures;

public sealed class IssuesUpsertTests : SqlServerIntegrationTestBase
{
    private ValueTask UpsertIssueAsync(
        int id,
        string status = "Open",
        int priority = 2,
        bool isOnHold = false,
        int? linkedPrNumber = null,
        in CancellationToken cancellationToken = default
    ) =>
        this.Database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.Issues_UpsertAsync(
                    connection: c,
                    repository: this.TestRepository,
                    id: id,
                    status: status,
                    priority: priority,
                    isOnHold: isOnHold,
                    linkedPrNumber: linkedPrNumber,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );

    private async ValueTask<IReadOnlyList<IssueRow>> GetActiveForTestRepoAsync()
    {
        IReadOnlyList<IssueRow> all = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.Issues_GetActiveAsync,
            cancellationToken: this.CancellationToken()
        );

        return this.ForTestRepo(all);
    }

    [Fact]
    public async Task Insert_CreatesRowAsync()
    {
        await this.UpsertIssueAsync(
            id: 20,
            priority: 3,
            linkedPrNumber: 55,
            cancellationToken: this.CancellationToken()
        );

        IssueRow row = Assert.Single(await this.GetActiveForTestRepoAsync());
        Assert.Equal(expected: 20, actual: row.Id);
        Assert.Equal(expected: "Open", actual: row.Status);
        Assert.Equal(expected: 3, actual: row.Priority);
        Assert.False(row.IsOnHold, "Issue should not be on hold after initial insert");
        Assert.Equal(expected: 55, actual: row.LinkedPrNumber);
    }

    [Fact]
    public async Task Update_ModifiesExistingRowAsync()
    {
        CancellationToken ct = this.CancellationToken();

        await this.UpsertIssueAsync(id: 21, priority: 1, cancellationToken: ct);
        await this.UpsertIssueAsync(id: 21, priority: 4, linkedPrNumber: 77, cancellationToken: ct);

        IssueRow row = Assert.Single(await this.GetActiveForTestRepoAsync());
        Assert.Equal(expected: 21, actual: row.Id);
        Assert.Equal(expected: 4, actual: row.Priority);
        Assert.Equal(expected: 77, actual: row.LinkedPrNumber);
    }

    [Fact]
    public async Task Update_PreservesFirstSeenAsync()
    {
        CancellationToken ct = this.CancellationToken();

        await this.UpsertIssueAsync(id: 22, priority: 2, cancellationToken: ct);

        IssueRow firstRow = Assert.Single(await this.GetActiveForTestRepoAsync());

        await this.UpsertIssueAsync(id: 22, priority: 5, cancellationToken: ct);

        IssueRow updatedRow = Assert.Single(await this.GetActiveForTestRepoAsync());

        Assert.Equal(expected: firstRow.FirstSeen, actual: updatedRow.FirstSeen);
        Assert.Equal(expected: 5, actual: updatedRow.Priority);
    }

    [Fact]
    public async Task Update_WithNullLinkedPrNumber_PreservesExistingValueAsync()
    {
        CancellationToken ct = this.CancellationToken();

        await this.UpsertIssueAsync(id: 23, priority: 2, linkedPrNumber: 42, cancellationToken: ct);
        await this.UpsertIssueAsync(id: 23, priority: 3, linkedPrNumber: null, cancellationToken: ct);

        IssueRow row = Assert.Single(await this.GetActiveForTestRepoAsync());
        Assert.Equal(expected: 42, actual: row.LinkedPrNumber);
    }
}
