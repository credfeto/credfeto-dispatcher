using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Database.Interfaces;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage.Database;

internal static partial class DispatcherDatabase
{
    [SqlObjectMap("PullRequests_GetActive", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask<IReadOnlyList<PullRequestRow>> PullRequests_GetActiveAsync(
        DbConnection connection,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("Issues_GetActive", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask<IReadOnlyList<IssueRow>> Issues_GetActiveAsync(
        DbConnection connection,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("Repos_GetActive", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask<IReadOnlyList<RepoRow>> Repos_GetActiveAsync(
        DbConnection connection,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("Repos_SetActive", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask Repos_SetActiveAsync(
        DbConnection connection,
        string repositories,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("PullRequests_Upsert", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask PullRequests_UpsertAsync(
        DbConnection connection,
        string repository,
        int id,
        string status,
        int priority,
        bool isOnHold,
        int commentCount,
        string? reviewDecision,
        int failedCheckCount,
        string? failedCheckNames,
        string? failedCheckSha,
        string? author,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("Issues_Upsert", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask Issues_UpsertAsync(
        DbConnection connection,
        string repository,
        int id,
        string status,
        int priority,
        bool isOnHold,
        int? linkedPrNumber,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("Issues_LinkPullRequest", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask Issues_LinkPullRequestAsync(
        DbConnection connection,
        string repository,
        int id,
        int linkedPrNumber,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("PollingStates_GetByKey", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask<PollingStateRow?> PollingStates_GetByKeyAsync(
        DbConnection connection,
        string key,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("PollingStates_Upsert", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask PollingStates_UpsertAsync(
        DbConnection connection,
        string key,
        string eTag,
        CancellationToken cancellationToken
    );

    [SqlObjectMap(
        "PullRequests_RemoveForRepositories",
        SqlObjectType.STORED_PROCEDURE,
        SqlDialect.MICROSOFT_SQL_SERVER
    )]
    public static partial ValueTask PullRequests_RemoveForRepositoriesAsync(
        DbConnection connection,
        string repositories,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("Issues_RemoveForRepositories", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask Issues_RemoveForRepositoriesAsync(
        DbConnection connection,
        string repositories,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("PullRequests_CloseStale", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask PullRequests_CloseStaleAsync(
        DbConnection connection,
        string repository,
        string? activePrIds,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("Issues_CloseStale", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask Issues_CloseStaleAsync(
        DbConnection connection,
        string repository,
        string? activeIssueIds,
        CancellationToken cancellationToken
    );
}
