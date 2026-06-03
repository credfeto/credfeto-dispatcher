using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.Database;
using Credfeto.Dispatcher.Storage.Database.Rows;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Integration.Tests.StoredProcedures;

public sealed class IssuesGetActiveTests : SqlServerIntegrationTestBase
{
    private ValueTask InsertIssueAsync(
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

    private ValueTask InsertOpenPullRequestAsync(int id, in CancellationToken cancellationToken = default) =>
        this.Database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.PullRequests_UpsertAsync(
                    connection: c,
                    repository: this.TestRepository,
                    id: id,
                    status: "Open",
                    priority: 2,
                    isOnHold: false,
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
    public async Task OpenIssue_IsReturnedAsync()
    {
        await this.InsertIssueAsync(id: 1, cancellationToken: this.CancellationToken());

        IReadOnlyList<IssueRow> rows = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.Issues_GetActiveAsync,
            cancellationToken: this.CancellationToken()
        );

        IssueRow row = Assert.Single(this.ForTestRepo(rows));
        Assert.Equal(expected: 1, actual: row.Id);
        Assert.Equal(expected: "Open", actual: row.Status);
        Assert.Null(row.LinkedPrNumber);
    }

    [Fact]
    public async Task ClosedIssue_IsExcludedAsync()
    {
        await this.InsertIssueAsync(id: 2, status: "Closed", cancellationToken: this.CancellationToken());

        IReadOnlyList<IssueRow> rows = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.Issues_GetActiveAsync,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(this.ForTestRepo(rows));
    }

    [Fact]
    public async Task OnHoldIssue_IsExcludedAsync()
    {
        await this.InsertIssueAsync(id: 3, isOnHold: true, cancellationToken: this.CancellationToken());

        IReadOnlyList<IssueRow> rows = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.Issues_GetActiveAsync,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(this.ForTestRepo(rows));
    }

    [Fact]
    public async Task IssueLinkedToOpenPullRequest_IsExcludedAsync()
    {
        CancellationToken ct = this.CancellationToken();
        await this.InsertOpenPullRequestAsync(id: 99, cancellationToken: ct);
        await this.InsertIssueAsync(id: 4, linkedPrNumber: 99, cancellationToken: ct);

        IReadOnlyList<IssueRow> rows = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.Issues_GetActiveAsync,
            cancellationToken: ct
        );

        Assert.Empty(this.ForTestRepo(rows));
    }

    [Fact]
    public async Task UrgentIssueLinkedToOpenPullRequest_IsAlwaysIncludedAsync()
    {
        const int URGENT_PRIORITY = 4;
        CancellationToken ct = this.CancellationToken();
        await this.InsertOpenPullRequestAsync(id: 99, cancellationToken: ct);
        await this.InsertIssueAsync(id: 5, priority: URGENT_PRIORITY, linkedPrNumber: 99, cancellationToken: ct);

        IReadOnlyList<IssueRow> rows = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.Issues_GetActiveAsync,
            cancellationToken: ct
        );

        IssueRow row = Assert.Single(this.ForTestRepo(rows));
        Assert.Equal(expected: 5, actual: row.Id);
        Assert.Equal(expected: URGENT_PRIORITY, actual: row.Priority);
    }

    [Fact]
    public async Task SecurityIssueWithOpenPullRequestInRepo_IsAlwaysIncludedAsync()
    {
        const int SECURITY_PRIORITY = 5;
        CancellationToken ct = this.CancellationToken();
        await this.InsertOpenPullRequestAsync(id: 98, cancellationToken: ct);
        await this.InsertIssueAsync(id: 6, priority: SECURITY_PRIORITY, cancellationToken: ct);

        IReadOnlyList<IssueRow> rows = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.Issues_GetActiveAsync,
            cancellationToken: ct
        );

        IReadOnlyList<IssueRow> testRows = this.ForTestRepo(rows);
        Assert.Contains(testRows, r => r.Id == 6 && r.Priority == SECURITY_PRIORITY);
    }

    [Fact]
    public async Task IssueForInactiveRepo_IsExcludedAsync()
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

        await this.InsertIssueAsync(id: 7, cancellationToken: ct);

        await this.Database.ExecuteAsync(
            action: (c, innerCt) =>
                DispatcherDatabase.Repos_SetActiveAsync(
                    connection: c,
                    repositories: otherRepo,
                    cancellationToken: innerCt
                ),
            cancellationToken: ct
        );

        IReadOnlyList<IssueRow> rows = await this.Database.ExecuteAsync(
            action: DispatcherDatabase.Issues_GetActiveAsync,
            cancellationToken: ct
        );

        Assert.Empty(this.ForTestRepo(rows));
    }
}
