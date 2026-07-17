using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Database;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage;

public sealed class WorkItemRepository : IWorkItemRepository
{
    private readonly IDatabase _database;
    private readonly TimeProvider _timeProvider;

    public WorkItemRepository(IDatabase database, TimeProvider timeProvider)
    {
        this._database = database;
        this._timeProvider = timeProvider;
    }

    public async Task<PrioritiesResponse> GetPrioritisedWorkItemsAsync(
        IReadOnlyList<string> owners,
        int maxIssues,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<PullRequestRow> prRows = await this._database.ExecuteAsync(
            action: DispatcherDatabase.PullRequests_GetActiveAsync,
            cancellationToken: cancellationToken
        );

        IReadOnlyList<IssueRow> issueRows = await this._database.ExecuteAsync(
            action: DispatcherDatabase.Issues_GetActiveAsync,
            cancellationToken: cancellationToken
        );

        return WorkItemMapping.BuildResponse(
            prRows: prRows,
            issueRows: issueRows,
            owners: owners,
            maxIssues: maxIssues,
            now: this._timeProvider.GetUtcNow()
        );
    }

    public async ValueTask RemoveItemsForRepositoriesAsync(
        IReadOnlyList<string> repositories,
        CancellationToken cancellationToken
    )
    {
        if (repositories.Count == 0)
        {
            return;
        }

        string reposCsv = string.Join(separator: ',', repositories);

        await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.PullRequests_RemoveForRepositoriesAsync(
                    connection: c,
                    repositories: reposCsv,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );

        await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.Issues_RemoveForRepositoriesAsync(
                    connection: c,
                    repositories: reposCsv,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }

    public async ValueTask CloseStaleItemsForRepoAsync(
        string repository,
        IReadOnlyList<int> activePullRequestNumbers,
        IReadOnlyList<int> activeIssueNumbers,
        CancellationToken cancellationToken
    )
    {
        string? activePrIds =
            activePullRequestNumbers.Count > 0 ? string.Join(separator: ',', activePullRequestNumbers) : null;
        string? activeIssueIds = activeIssueNumbers.Count > 0 ? string.Join(separator: ',', activeIssueNumbers) : null;

        await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.PullRequests_CloseStaleAsync(
                    connection: c,
                    repository: repository,
                    activePrIds: activePrIds,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );

        await this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.Issues_CloseStaleAsync(
                    connection: c,
                    repository: repository,
                    activeIssueIds: activeIssueIds,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }
}
