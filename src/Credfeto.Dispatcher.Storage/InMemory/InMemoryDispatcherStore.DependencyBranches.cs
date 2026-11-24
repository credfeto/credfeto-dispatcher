using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage.InMemory;

// The DependencyBranches table and its procedures (migration 021), with the same rules.
public sealed partial class InMemoryDispatcherStore
{
    private readonly Dictionary<(string Repository, string Branch), DependencyBranchRow> _dependencyBranches = [];

    public void RecordDependencyBranchCreated(string repository, string branch, in DateTimeOffset createdAt)
    {
        lock (this._gate)
        {
            (string Repository, string Branch) key = (repository, branch);

            if (
                this._dependencyBranches.TryGetValue(key, out DependencyBranchRow? existing)
                && (existing.DeletedAt is not { } deletedAt || deletedAt >= createdAt)
            )
            {
                return;
            }

            this._version++;
            this._dependencyBranches[key] = new DependencyBranchRow(
                Repository: repository,
                Branch: branch,
                FirstSeen: createdAt,
                LastChecked: null,
                LastUpdated: this._timeProvider.GetUtcNow(),
                PullRequestNumber: null,
                DeletedAt: null,
                ETag: null
            );
        }
    }

    public void RecordDependencyBranchDeleted(string repository, string branch, in DateTimeOffset deletedAt)
    {
        lock (this._gate)
        {
            (string Repository, string Branch) key = (repository, branch);

            if (
                !this._dependencyBranches.TryGetValue(key, out DependencyBranchRow? existing)
                || existing.DeletedAt is not null
                || existing.FirstSeen > deletedAt
            )
            {
                return;
            }

            this._version++;
            this._dependencyBranches[key] = existing with
            {
                DeletedAt = deletedAt,
                LastUpdated = this._timeProvider.GetUtcNow(),
            };
        }
    }

    public void RecordDependencyBranchPullRequest(string repository, string branch, int pullRequestNumber)
    {
        lock (this._gate)
        {
            (string Repository, string Branch) key = (repository, branch);

            if (!this._dependencyBranches.TryGetValue(key, out DependencyBranchRow? existing))
            {
                return;
            }

            this._version++;
            this._dependencyBranches[key] = existing with
            {
                PullRequestNumber = pullRequestNumber,
                LastUpdated = this._timeProvider.GetUtcNow(),
            };
        }
    }

    public void RecordDependencyBranchChecked(string repository, string branch, string? eTag)
    {
        lock (this._gate)
        {
            (string Repository, string Branch) key = (repository, branch);

            if (!this._dependencyBranches.TryGetValue(key, out DependencyBranchRow? existing))
            {
                return;
            }

            this._version++;
            this._dependencyBranches[key] = existing with
            {
                LastChecked = this._timeProvider.GetUtcNow(),
                ETag = eTag ?? existing.ETag,
            };
        }
    }

    public void PruneDependencyBranches(in DateTimeOffset settledBefore, in DateTimeOffset firstSeenBefore)
    {
        DateTimeOffset settledCutOff = settledBefore;
        DateTimeOffset firstSeenCutOff = firstSeenBefore;

        lock (this._gate)
        {
            (string Repository, string Branch)[] expired =
            [
                .. this
                    ._dependencyBranches.Where(entry =>
                        IsSettled(entry.Value) && entry.Value.LastUpdated < settledCutOff
                        || entry.Value.FirstSeen < firstSeenCutOff
                    )
                    .Select(static entry => entry.Key),
            ];

            if (expired.Length == 0)
            {
                return;
            }

            this._version++;

            foreach ((string Repository, string Branch) key in expired)
            {
                this._dependencyBranches.Remove(key);
            }
        }
    }

    internal IReadOnlyList<DependencyBranchRow> GetDependencyBranchesDueForRecheck(
        int maxCount,
        in DateTimeOffset checkedBefore
    )
    {
        if (maxCount <= 0)
        {
            return [];
        }

        DateTimeOffset cutOff = checkedBefore;

        lock (this._gate)
        {
            return
            [
                .. this
                    ._dependencyBranches.Values.Where(row => IsUnresolved(row) && LastLookedAt(row) < cutOff)
                    .OrderBy(LastLookedAt)
                    .ThenBy(static row => row.Repository, comparer: StringComparer.Ordinal)
                    .ThenBy(static row => row.Branch, comparer: StringComparer.Ordinal)
                    .Take(maxCount),
            ];
        }
    }

    internal IReadOnlyList<DependencyBranchRow> GetOrphanedDependencyBranches(
        string? owner,
        string? repo,
        in DateTimeOffset firstSeenBefore
    )
    {
        DateTimeOffset cutOff = firstSeenBefore;

        lock (this._gate)
        {
            return
            [
                .. this
                    ._dependencyBranches.Values.Where(row =>
                        IsUnresolved(row)
                        && row.FirstSeen < cutOff
                        && MatchesRepository(repository: row.Repository, owner: owner, repo: repo)
                    )
                    .OrderBy(static row => row.FirstSeen)
                    .ThenBy(static row => row.Repository, comparer: StringComparer.Ordinal)
                    .ThenBy(static row => row.Branch, comparer: StringComparer.Ordinal),
            ];
        }
    }

    private static bool IsUnresolved(DependencyBranchRow row)
    {
        return row.PullRequestNumber is null && row.DeletedAt is null;
    }

    private static bool IsSettled(DependencyBranchRow row)
    {
        return !IsUnresolved(row);
    }

    private static DateTimeOffset LastLookedAt(DependencyBranchRow row)
    {
        return row.LastChecked ?? row.FirstSeen;
    }

    private DependencyBranchRow[] ExportDependencyBranchesNoLock()
    {
        return [.. this._dependencyBranches.Values];
    }

    // Snapshots written before dependency branches were tracked have none.
    private void ImportDependencyBranchesNoLock(DependencyBranchRow[]? source)
    {
        this._dependencyBranches.Clear();

        foreach (DependencyBranchRow row in source ?? [])
        {
            this._dependencyBranches[(row.Repository, row.Branch)] = row;
        }
    }
}
