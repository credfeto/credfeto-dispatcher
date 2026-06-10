using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.Database;
using Credfeto.Dispatcher.Storage.Database.Rows;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Integration.Tests.StoredProcedures;

public sealed class PullRequestsUpsertTests : SqlServerIntegrationTestBase
{
    private ValueTask UpsertPullRequestAsync(
        int id,
        string status = "Open",
        int priority = 2,
        bool isOnHold = false,
        int commentCount = 0,
        string? reviewDecision = null,
        int failedCheckCount = 0,
        string? failedCheckNames = null,
        string? failedCheckSha = null,
        string? author = null,
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
                    commentCount: commentCount,
                    reviewDecision: reviewDecision,
                    failedCheckCount: failedCheckCount,
                    failedCheckNames: failedCheckNames,
                    failedCheckSha: failedCheckSha,
                    author: author,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );

    [Fact]
    public async Task Insert_CreatesRowAsync()
    {
        await this.UpsertPullRequestAsync(
            id: 10,
            status: "Open",
            priority: 3,
            commentCount: 5,
            reviewDecision: "Approved",
            failedCheckCount: 2,
            failedCheckNames: "check-a,check-b",
            failedCheckSha: "abc123",
            author: "dev-user",
            cancellationToken: this.CancellationToken()
        );

        PullRequestRow row = Assert.Single(await this.GetActivePullRequestsForTestRepoAsync());
        Assert.Equal(expected: 10, actual: row.Id);
        Assert.Equal(expected: "Open", actual: row.Status);
        Assert.Equal(expected: 3, actual: row.Priority);
        Assert.False(row.IsOnHold, "PR should not be on hold after initial insert");
        Assert.Equal(expected: 5, actual: row.CommentCount);
        Assert.Equal(expected: "Approved", actual: row.ReviewDecision);
        Assert.Equal(expected: 2, actual: row.FailedCheckCount);
        Assert.Equal(expected: "check-a,check-b", actual: row.FailedCheckNames);
        Assert.Equal(expected: "abc123", actual: row.FailedCheckSha);
        Assert.Equal(expected: "dev-user", actual: row.Author);
    }

    [Fact]
    public async Task Update_ModifiesExistingRowAsync()
    {
        CancellationToken ct = this.CancellationToken();

        await this.UpsertPullRequestAsync(id: 11, priority: 1, cancellationToken: ct);

        await this.UpsertPullRequestAsync(
            id: 11,
            priority: 3,
            commentCount: 7,
            reviewDecision: "ChangesRequested",
            author: "updated-author",
            cancellationToken: ct
        );

        PullRequestRow row = Assert.Single(await this.GetActivePullRequestsForTestRepoAsync());
        Assert.Equal(expected: 11, actual: row.Id);
        Assert.Equal(expected: 3, actual: row.Priority);
        Assert.Equal(expected: 7, actual: row.CommentCount);
        Assert.Equal(expected: "ChangesRequested", actual: row.ReviewDecision);
        Assert.Equal(expected: "updated-author", actual: row.Author);
    }

    [Fact]
    public async Task Update_PreservesFirstSeenAsync()
    {
        CancellationToken ct = this.CancellationToken();

        await this.UpsertPullRequestAsync(id: 12, priority: 2, cancellationToken: ct);

        PullRequestRow firstRow = Assert.Single(await this.GetActivePullRequestsForTestRepoAsync());

        await this.UpsertPullRequestAsync(id: 12, priority: 4, commentCount: 3, cancellationToken: ct);

        PullRequestRow updatedRow = Assert.Single(await this.GetActivePullRequestsForTestRepoAsync());

        Assert.Equal(expected: firstRow.FirstSeen, actual: updatedRow.FirstSeen);
        Assert.Equal(expected: 4, actual: updatedRow.Priority);
    }
}
