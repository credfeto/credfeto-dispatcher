using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

// Dependency branches (depends/*, dependabot/*) seen being created, so one that never gets a pull request shows up.
public interface IDependencyBranchStore
{
    // Starts tracking a branch. A branch already tracked is left alone unless it was deleted before createdAt, when
    // it is tracked afresh.
    ValueTask RecordCreatedAsync(
        string repository,
        string branch,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken
    );

    ValueTask RecordDeletedAsync(
        string repository,
        string branch,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken
    );

    // A pull request whose head is the branch resolves it; an untracked branch is ignored.
    ValueTask RecordPullRequestAsync(
        string repository,
        string branch,
        int pullRequestNumber,
        CancellationToken cancellationToken
    );

    // Unresolved, undeleted branches last checked (or, never checked, first seen) before checkedBefore, oldest first.
    ValueTask<IReadOnlyList<DependencyBranchRecheck>> GetDueForRecheckAsync(
        int maxCount,
        DateTimeOffset checkedBefore,
        CancellationToken cancellationToken
    );

    // A null eTag keeps the stored one.
    ValueTask RecordCheckedAsync(string repository, string branch, string? eTag, CancellationToken cancellationToken);

    // Removes resolved or deleted branches not updated since settledBefore, and any branch first seen before
    // firstSeenBefore, so the table stays bounded.
    ValueTask PruneAsync(
        DateTimeOffset settledBefore,
        DateTimeOffset firstSeenBefore,
        CancellationToken cancellationToken
    );
}
