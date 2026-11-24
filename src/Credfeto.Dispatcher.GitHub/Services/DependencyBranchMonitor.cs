using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Models;
using Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.Services;

// Tracks dependency branches (depends/*, dependabot/*) from their CreateEvent so that one which never gets a pull
// request shows up in /db. A PR upserted for the branch resolves it (see the storage trackers); a DeleteEvent ends it.
// Each poll re-checks a few branches still without a PR, least recently checked first, with one conditional request
// each, and prunes settled rows so the table stays bounded. Report only: nothing here alerts on or acts on a branch.
public sealed class DependencyBranchMonitor : IDependencyBranchMonitor
{
    // A branch is not re-checked more often than this; the create-time lookup covers its first minutes.
    internal static readonly TimeSpan RecheckInterval = TimeSpan.FromMinutes(15);

    // Resolved or deleted branches are kept this long after their last change, then removed.
    internal static readonly TimeSpan SettledRetention = TimeSpan.FromDays(7);

    // Backstop for a branch whose deletion was never seen: by now the stale-branch recovery has opened a PR for it.
    internal static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    private readonly GitHubRepoHelper _helper;
    private readonly ILogger<DependencyBranchMonitor> _logger;
    private readonly GitHubOptions _options;
    private readonly IDependencyBranchStore _store;
    private readonly TimeProvider _timeProvider;

    public DependencyBranchMonitor(
        GitHubRepoHelper helper,
        IDependencyBranchStore store,
        IOptions<GitHubOptions> options,
        TimeProvider timeProvider,
        ILogger<DependencyBranchMonitor> logger
    )
    {
        this._helper = helper;
        this._store = store;
        this._options = options.Value;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    public async ValueTask UpdateAsync(
        IReadOnlyList<DependencyBranchChange> changes,
        CancellationToken cancellationToken
    )
    {
        foreach (DependencyBranchChange change in changes)
        {
            await this.ApplyChangeAsync(change: change, cancellationToken: cancellationToken);
        }

        await this.RecheckAsync(cancellationToken);

        DateTimeOffset now = this._timeProvider.GetUtcNow();

        await this._store.PruneAsync(
            settledBefore: now - SettledRetention,
            firstSeenBefore: now - MaxAge,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask ApplyChangeAsync(DependencyBranchChange change, CancellationToken cancellationToken)
    {
        if (change.IsDeleted)
        {
            await this._store.RecordDeletedAsync(
                repository: change.Repository,
                branch: change.Branch,
                deletedAt: change.At,
                cancellationToken: cancellationToken
            );
        }
        else
        {
            await this._store.RecordCreatedAsync(
                repository: change.Repository,
                branch: change.Branch,
                createdAt: change.At,
                cancellationToken: cancellationToken
            );
        }

        this._logger.LogDependencyBranchChange(
            action: change.IsDeleted ? "deleted" : "created",
            repo: change.Repository,
            branch: change.Branch
        );
    }

    private async ValueTask RecheckAsync(CancellationToken cancellationToken)
    {
        int maxCount = this._options.Filter.MaxDependencyBranchRechecksPerPoll;

        if (maxCount <= 0)
        {
            return;
        }

        IReadOnlyList<DependencyBranchRecheck> due = await this._store.GetDueForRecheckAsync(
            maxCount: maxCount,
            checkedBefore: this._timeProvider.GetUtcNow() - RecheckInterval,
            cancellationToken: cancellationToken
        );

        if (due.Count == 0)
        {
            return;
        }

        int checkedCount = 0;
        int resolvedCount = 0;

        foreach (DependencyBranchRecheck branch in due.Take(maxCount))
        {
            bool? resolved = await this.RecheckBranchAsync(branch: branch, cancellationToken: cancellationToken);

            if (resolved is null)
            {
                break;
            }

            ++checkedCount;
            resolvedCount += resolved.Value ? 1 : 0;
        }

        this._logger.LogDependencyBranchRecheckComplete(checkedCount: checkedCount, resolvedCount: resolvedCount);
    }

    // True when a PR for the branch was found, false when not, null when rate limited (the branch stays due).
    private async ValueTask<bool?> RecheckBranchAsync(
        DependencyBranchRecheck branch,
        CancellationToken cancellationToken
    )
    {
        PagedETagResult<ApiPullRequest> result = await this._helper.GetPagedWithETagAsync(
            url: BuildPullRequestsForBranchUrl(repo: branch.Repository, branch: branch.Branch),
            jsonTypeInfo: NotificationSerializerContext.Default.ApiPullRequestArray,
            eTag: branch.ETag,
            cancellationToken: cancellationToken
        );

        if (result.FailureStatus is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
        {
            this._logger.LogDependencyBranchRecheckRateLimited(repo: branch.Repository, branch: branch.Branch);

            return null;
        }

        bool resolved = result.Items is { Length: > 0 };

        if (result.Items is { Length: > 0 } items)
        {
            int number = items.Max(static pr => pr.Number);

            await this._store.RecordPullRequestAsync(
                repository: branch.Repository,
                branch: branch.Branch,
                pullRequestNumber: number,
                cancellationToken: cancellationToken
            );

            this._logger.LogDependencyBranchPullRequestFound(
                repo: branch.Repository,
                number: number,
                branch: branch.Branch
            );
        }

        await this._store.RecordCheckedAsync(
            repository: branch.Repository,
            branch: branch.Branch,
            eTag: result.NotModified ? null : result.ETag,
            cancellationToken: cancellationToken
        );

        return resolved;
    }

    // Any state: a PR that was opened and then closed or merged still means the branch got one.
    internal static string BuildPullRequestsForBranchUrl(string repo, string branch)
    {
        int slash = repo.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);
        string owner = slash >= 0 ? repo[..slash] : repo;
        string head = string.Concat(Uri.EscapeDataString(owner), ":", Uri.EscapeDataString(branch));

        return $"repos/{repo}/pulls?state=all&head={head}&per_page=10";
    }
}
