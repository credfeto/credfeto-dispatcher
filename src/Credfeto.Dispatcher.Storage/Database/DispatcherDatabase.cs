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
    [SqlObjectMap("WorkItems_GetPrioritised", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask<IReadOnlyList<WorkItemRow>> WorkItems_GetPrioritisedAsync(
        DbConnection connection,
        string owners,
        string boostedRepos,
        int maxIssues,
        string? user,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("PullRequests_GetAll", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask<IReadOnlyList<PullRequestStateRow>> PullRequests_GetAllAsync(
        DbConnection connection,
        string? owner,
        string? repo,
        string? user,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("Issues_GetAll", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask<IReadOnlyList<IssueStateRow>> Issues_GetAllAsync(
        DbConnection connection,
        string? owner,
        string? repo,
        string? user,
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
        bool hasDetail,
        int commentCount,
        string? reviewDecision,
        int failedCheckCount,
        string? failedCheckNames,
        string? failedCheckSha,
        string? author,
        bool? isAdopted,
        string? assignees,
        string? commitAuthors,
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
        string? assignees,
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

    [SqlObjectMap("DependencyBranches_RecordCreated", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask DependencyBranches_RecordCreatedAsync(
        DbConnection connection,
        string repository,
        string branch,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("DependencyBranches_RecordDeleted", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask DependencyBranches_RecordDeletedAsync(
        DbConnection connection,
        string repository,
        string branch,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken
    );

    [SqlObjectMap(
        "DependencyBranches_RecordPullRequest",
        SqlObjectType.STORED_PROCEDURE,
        SqlDialect.MICROSOFT_SQL_SERVER
    )]
    public static partial ValueTask DependencyBranches_RecordPullRequestAsync(
        DbConnection connection,
        string repository,
        string branch,
        int pullRequestNumber,
        CancellationToken cancellationToken
    );

    [SqlObjectMap(
        "DependencyBranches_GetDueForRecheck",
        SqlObjectType.STORED_PROCEDURE,
        SqlDialect.MICROSOFT_SQL_SERVER
    )]
    public static partial ValueTask<IReadOnlyList<DependencyBranchRow>> DependencyBranches_GetDueForRecheckAsync(
        DbConnection connection,
        int maxCount,
        DateTimeOffset checkedBefore,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("DependencyBranches_RecordChecked", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask DependencyBranches_RecordCheckedAsync(
        DbConnection connection,
        string repository,
        string branch,
        string? eTag,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("DependencyBranches_Prune", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask DependencyBranches_PruneAsync(
        DbConnection connection,
        DateTimeOffset settledBefore,
        DateTimeOffset firstSeenBefore,
        CancellationToken cancellationToken
    );

    [SqlObjectMap("DependencyBranches_GetOrphaned", SqlObjectType.STORED_PROCEDURE, SqlDialect.MICROSOFT_SQL_SERVER)]
    public static partial ValueTask<IReadOnlyList<DependencyBranchRow>> DependencyBranches_GetOrphanedAsync(
        DbConnection connection,
        string? owner,
        string? repo,
        DateTimeOffset firstSeenBefore,
        CancellationToken cancellationToken
    );
}
