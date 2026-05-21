using System;
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

    [SqlObjectMap("Repos_SetActive", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask Repos_SetActiveAsync(
        DbConnection connection,
        string repositories,
        DateTimeOffset lastUpdated,
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
        string? headBranchName,
        DateTimeOffset now,
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
        DateTimeOffset now,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("NotificationQueue_Upsert", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask NotificationQueue_UpsertAsync(
        DbConnection connection,
        string subjectUrl,
        string notificationId,
        string repository,
        string repositoryUrl,
        string subjectType,
        string subjectTitle,
        string reason,
        DateTimeOffset updatedAt,
        DateTimeOffset queuedAt,
        DateTimeOffset dispatchAfter,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("NotificationQueue_Delete", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask NotificationQueue_DeleteAsync(
        DbConnection connection,
        string subjectUrl,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("NotificationQueue_GetReady", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask<IReadOnlyList<NotificationQueueRow>> NotificationQueue_GetReadyAsync(
        DbConnection connection,
        DateTimeOffset now,
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
        DateTimeOffset now,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("Issues_CloseStale", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask Issues_CloseStaleAsync(
        DbConnection connection,
        string repository,
        string? activeIssueIds,
        DateTimeOffset now,
        CancellationToken cancellationToken
    );
}
