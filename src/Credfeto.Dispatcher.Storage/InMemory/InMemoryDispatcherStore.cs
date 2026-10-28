using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage.InMemory;

// Repository names are compared ordinally (case-sensitively): unlike SQL Server's default
// case-insensitive collation, but every writer here sources the name from the GitHub API,
// which is already canonical, so the two never disagree in practice.
public sealed class InMemoryDispatcherStore
{
    private const string OPEN_STATUS = "Open";
    private const string DRAFT_STATUS = "Draft";
    private const string CLOSED_STATUS = "Closed";

    private readonly Lock _gate = new();
    private readonly Dictionary<string, bool> _repos = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Repository, int Id), PullRequestRow> _pullRequests = [];
    private readonly Dictionary<(string Repository, int Id), IssueRow> _issues = [];
    private readonly Dictionary<(string Repository, int Id), string[]> _pullRequestAssignees = [];
    private readonly Dictionary<(string Repository, int Id), string[]> _issueAssignees = [];
    private readonly Dictionary<(string Repository, int Id), string[]> _pullRequestCommitAuthors = [];
    private readonly Dictionary<string, string> _pollingStates = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    // Bumped on every mutating call (under _gate), so SnapshotWriterService can skip writing a
    // snapshot when nothing has changed since the last write - never read as a precise change
    // count, only ever compared for equality against a previously observed value.
    private int _version;

    public InMemoryDispatcherStore(TimeProvider timeProvider)
    {
        this._timeProvider = timeProvider;
    }

    internal int Version
    {
        get
        {
            lock (this._gate)
            {
                return this._version;
            }
        }
    }

    public IReadOnlyList<string> GetActiveRepos()
    {
        lock (this._gate)
        {
            return [.. this._repos.Where(static kvp => kvp.Value).Select(static kvp => kvp.Key)];
        }
    }

    public void SetActiveRepos(IReadOnlyList<string> activeRepos)
    {
        lock (this._gate)
        {
            this._version++;

            HashSet<string> active = new(activeRepos, StringComparer.Ordinal);

            foreach (string repo in active)
            {
                this._repos[repo] = true;
            }

            foreach (string repo in this._repos.Keys.Where(repo => !active.Contains(repo)).ToArray())
            {
                this._repos[repo] = false;
            }
        }
    }

    public string? GetETag(string key)
    {
        lock (this._gate)
        {
            return this._pollingStates.GetValueOrDefault(key);
        }
    }

    public void SaveETag(string key, string eTag)
    {
        lock (this._gate)
        {
            this._version++;
            this._pollingStates[key] = eTag;
        }
    }

    public void UpsertPullRequest(
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
        IReadOnlyList<string>? assignees,
        IReadOnlyList<string>? commitAuthors
    )
    {
        lock (this._gate)
        {
            (string Repository, int Id) key = (repository, id);
            (DateTimeOffset now, bool isClosed) = this.BeginUpsertNoLock(
                status: status,
                assigneeTarget: this._pullRequestAssignees,
                key: key,
                assignees: assignees
            );

            // Null keeps / empty clears, as for assignees; trackers pass null when the authors are unknown.
            ReplaceAssignees(target: this._pullRequestCommitAuthors, key: key, assignees: commitAuthors);

            if (this._pullRequests.TryGetValue(key, out PullRequestRow? existing))
            {
                this._pullRequests[key] = existing with
                {
                    Status = status,
                    Priority = priority,
                    IsOnHold = isOnHold,
                    CommentCount = hasDetail ? commentCount : existing.CommentCount,
                    ReviewDecision = hasDetail ? reviewDecision : existing.ReviewDecision,
                    FailedCheckCount = hasDetail ? failedCheckCount : existing.FailedCheckCount,
                    FailedCheckNames = hasDetail ? failedCheckNames : existing.FailedCheckNames,
                    FailedCheckSha = hasDetail ? failedCheckSha : existing.FailedCheckSha,
                    Author = author ?? existing.Author,
                    IsAdopted = isAdopted ?? existing.IsAdopted,
                    LastUpdated = now,
                    WhenClosed = isClosed ? existing.WhenClosed ?? now : null,
                };

                return;
            }

            this._pullRequests[key] = new PullRequestRow(
                Repository: repository,
                Id: id,
                Status: status,
                FirstSeen: now,
                LastUpdated: now,
                WhenClosed: isClosed ? now : null,
                Priority: priority,
                IsOnHold: isOnHold,
                CommentCount: commentCount,
                ReviewDecision: reviewDecision,
                FailedCheckCount: failedCheckCount,
                FailedCheckNames: failedCheckNames,
                FailedCheckSha: failedCheckSha,
                Author: author,
                IsAdopted: isAdopted ?? false
            );
        }
    }

    public void UpsertIssue(
        string repository,
        int id,
        string status,
        int priority,
        bool isOnHold,
        int? linkedPrNumber,
        IReadOnlyList<string>? assignees
    )
    {
        lock (this._gate)
        {
            (string Repository, int Id) key = (repository, id);
            (DateTimeOffset now, bool isClosed) = this.BeginUpsertNoLock(
                status: status,
                assigneeTarget: this._issueAssignees,
                key: key,
                assignees: assignees
            );

            if (this._issues.TryGetValue(key, out IssueRow? existing))
            {
                this._issues[key] = existing with
                {
                    Status = status,
                    Priority = priority,
                    IsOnHold = isOnHold,
                    LinkedPrNumber = linkedPrNumber ?? existing.LinkedPrNumber,
                    LastUpdated = now,
                    WhenClosed = isClosed ? existing.WhenClosed ?? now : null,
                };

                return;
            }

            this._issues[key] = new IssueRow(
                Repository: repository,
                Id: id,
                Status: status,
                FirstSeen: now,
                LastUpdated: now,
                WhenClosed: isClosed ? now : null,
                Priority: priority,
                IsOnHold: isOnHold,
                LinkedPrNumber: linkedPrNumber
            );
        }
    }

    private (DateTimeOffset Now, bool IsClosed) BeginUpsertNoLock(
        string status,
        Dictionary<(string Repository, int Id), string[]> assigneeTarget,
        (string Repository, int Id) key,
        IReadOnlyList<string>? assignees
    )
    {
        this._version++;

        ReplaceAssignees(target: assigneeTarget, key: key, assignees: assignees);

        return (this._timeProvider.GetUtcNow(), string.Equals(status, CLOSED_STATUS, StringComparison.Ordinal));
    }

    public void LinkIssueToPullRequest(string repository, int id, int linkedPrNumber)
    {
        lock (this._gate)
        {
            (string Repository, int Id) key = (repository, id);

            if (
                this._issues.TryGetValue(key, out IssueRow? existing)
                && string.Equals(existing.Status, OPEN_STATUS, StringComparison.Ordinal)
            )
            {
                this._version++;

                this._issues[key] = existing with
                {
                    LinkedPrNumber = linkedPrNumber,
                    LastUpdated = this._timeProvider.GetUtcNow(),
                };
            }
        }
    }

    internal (IReadOnlyList<PullRequestRow> PullRequests, IReadOnlyList<IssueRow> Issues) GetActiveWorkItems(
        string? user
    )
    {
        lock (this._gate)
        {
            IReadOnlyList<PullRequestRow> activePullRequests =
            [
                .. this._pullRequests.Values.Where(pr =>
                    IsOpenOrDraft(pr.Status)
                    && !pr.IsOnHold
                    && !this.IsRepoExplicitlyInactiveNoLock(pr.Repository)
                    && (
                        pr.IsAdopted
                        || IsVisibleTo(
                            user: user,
                            assigneesByItem: this._pullRequestAssignees,
                            key: (pr.Repository, pr.Id)
                        )
                    )
                ),
            ];

            IReadOnlyList<IssueRow> activeIssues =
            [
                .. this._issues.Values.Where(issue =>
                    string.Equals(issue.Status, OPEN_STATUS, StringComparison.Ordinal)
                    && !issue.IsOnHold
                    && !this.IsRepoExplicitlyInactiveNoLock(issue.Repository)
                    && !this.IsLinkedPullRequestActiveNoLock(issue.Repository, issue.LinkedPrNumber)
                    && (
                        issue.Priority >= (int)WorkPriority.URGENT
                        || !this.HasActivePullRequestNoLock(repository: issue.Repository, user: user)
                    )
                    && IsVisibleTo(user: user, assigneesByItem: this._issueAssignees, key: (issue.Repository, issue.Id))
                ),
            ];

            return (activePullRequests, activeIssues);
        }
    }

    internal (IReadOnlyList<PullRequestStateRow> PullRequests, IReadOnlyList<IssueStateRow> Issues) GetAllWorkItems(
        string? owner,
        string? repo,
        string? user
    )
    {
        lock (this._gate)
        {
            IReadOnlyList<PullRequestStateRow> pullRequests =
            [
                .. this
                    ._pullRequests.Values.Where(pr =>
                        MatchesRepository(repository: pr.Repository, owner: owner, repo: repo)
                        && (
                            pr.IsAdopted
                            || IsVisibleTo(
                                user: user,
                                assigneesByItem: this._pullRequestAssignees,
                                key: (pr.Repository, pr.Id)
                            )
                        )
                    )
                    .OrderBy(static pr => pr.Repository, comparer: StringComparer.Ordinal)
                    .ThenBy(static pr => pr.Id)
                    .Select(pr => ToStateRow(row: pr, assignees: this._pullRequestAssignees)),
            ];

            IReadOnlyList<IssueStateRow> issues =
            [
                .. this
                    ._issues.Values.Where(issue =>
                        MatchesRepository(repository: issue.Repository, owner: owner, repo: repo)
                        && IsVisibleTo(
                            user: user,
                            assigneesByItem: this._issueAssignees,
                            key: (issue.Repository, issue.Id)
                        )
                    )
                    .OrderBy(static issue => issue.Repository, comparer: StringComparer.Ordinal)
                    .ThenBy(static issue => issue.Id)
                    .Select(issue => ToStateRow(row: issue, assignees: this._issueAssignees)),
            ];

            return (pullRequests, issues);
        }
    }

    private static bool MatchesRepository(string repository, string? owner, string? repo)
    {
        if (owner is null && repo is null)
        {
            return true;
        }

        (string repositoryOwner, string repositoryName) = WorkItemMapping.SplitRepository(repository);

        return MatchesFilter(value: repositoryOwner, filter: owner)
            && MatchesFilter(value: repositoryName, filter: repo);
    }

    private static bool MatchesFilter(string value, string? filter)
    {
        return filter is null || string.Equals(a: value, b: filter, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static string? JoinAssignees(
        Dictionary<(string Repository, int Id), string[]> assignees,
        (string Repository, int Id) key
    )
    {
        return assignees.TryGetValue(key: key, out string[]? logins)
            ? string.Join(separator: ',', values: logins.Order(StringComparer.Ordinal))
            : null;
    }

    private static PullRequestStateRow ToStateRow(
        PullRequestRow row,
        Dictionary<(string Repository, int Id), string[]> assignees
    )
    {
        return new PullRequestStateRow(
            Repository: row.Repository,
            Id: row.Id,
            Status: row.Status,
            FirstSeen: row.FirstSeen,
            LastUpdated: row.LastUpdated,
            WhenClosed: row.WhenClosed,
            Priority: row.Priority,
            IsOnHold: row.IsOnHold,
            CommentCount: row.CommentCount,
            ReviewDecision: row.ReviewDecision,
            FailedCheckCount: row.FailedCheckCount,
            FailedCheckNames: row.FailedCheckNames,
            FailedCheckSha: row.FailedCheckSha,
            Author: row.Author,
            IsAdopted: row.IsAdopted,
            Assignees: JoinAssignees(assignees: assignees, key: (row.Repository, row.Id))
        );
    }

    private static IssueStateRow ToStateRow(IssueRow row, Dictionary<(string Repository, int Id), string[]> assignees)
    {
        return new IssueStateRow(
            Repository: row.Repository,
            Id: row.Id,
            Status: row.Status,
            FirstSeen: row.FirstSeen,
            LastUpdated: row.LastUpdated,
            WhenClosed: row.WhenClosed,
            Priority: row.Priority,
            IsOnHold: row.IsOnHold,
            LinkedPrNumber: row.LinkedPrNumber,
            Assignees: JoinAssignees(assignees: assignees, key: (row.Repository, row.Id))
        );
    }

    public void CloseStalePullRequests(string repository, IReadOnlyCollection<int>? activeIds)
    {
        lock (this._gate)
        {
            this._version++;

            DateTimeOffset now = this._timeProvider.GetUtcNow();

            foreach (
                (string Repository, int Id) key in this
                    ._pullRequests.Keys.Where(k => string.Equals(k.Repository, repository, StringComparison.Ordinal))
                    .ToArray()
            )
            {
                PullRequestRow row = this._pullRequests[key];

                if (!IsOpenOrDraft(row.Status) || activeIds is not null && activeIds.Contains(row.Id))
                {
                    continue;
                }

                this._pullRequests[key] = row with
                {
                    Status = CLOSED_STATUS,
                    WhenClosed = row.WhenClosed ?? now,
                    LastUpdated = now,
                };
            }
        }
    }

    public void CloseStaleIssues(string repository, IReadOnlyCollection<int>? activeIds)
    {
        lock (this._gate)
        {
            this._version++;

            DateTimeOffset now = this._timeProvider.GetUtcNow();

            foreach (
                (string Repository, int Id) key in this
                    ._issues.Keys.Where(k => string.Equals(k.Repository, repository, StringComparison.Ordinal))
                    .ToArray()
            )
            {
                IssueRow row = this._issues[key];

                if (
                    !string.Equals(row.Status, OPEN_STATUS, StringComparison.Ordinal)
                    || activeIds is not null && activeIds.Contains(row.Id)
                )
                {
                    continue;
                }

                this._issues[key] = row with
                {
                    Status = CLOSED_STATUS,
                    WhenClosed = row.WhenClosed ?? now,
                    LastUpdated = now,
                };
            }
        }
    }

    public void RemoveForRepositories(IReadOnlyCollection<string> repositories)
    {
        if (repositories.Count == 0)
        {
            return;
        }

        lock (this._gate)
        {
            this._version++;

            HashSet<string> repos = new(repositories, StringComparer.Ordinal);

            RemoveForRepositories(target: this._pullRequests, repos: repos);
            RemoveForRepositories(target: this._issues, repos: repos);
            RemoveForRepositories(target: this._pullRequestAssignees, repos: repos);
            RemoveForRepositories(target: this._issueAssignees, repos: repos);
            RemoveForRepositories(target: this._pullRequestCommitAuthors, repos: repos);
        }
    }

    private static void RemoveForRepositories<TValue>(
        Dictionary<(string Repository, int Id), TValue> target,
        HashSet<string> repos
    )
    {
        foreach ((string Repository, int Id) key in target.Keys.Where(k => repos.Contains(k.Repository)).ToArray())
        {
            target.Remove(key);
        }
    }

    // Copies all store state to a snapshot under _gate, so a concurrent mutation during
    // the caller's later async serialisation can't race the unsynchronised source dictionaries.
    // Called periodically by SnapshotWriterService.
    internal DispatcherStoreSnapshotData ExportSnapshot()
    {
        lock (this._gate)
        {
            return new DispatcherStoreSnapshotData(
                Repos: new Dictionary<string, bool>(this._repos, StringComparer.Ordinal),
                PullRequests: [.. this._pullRequests.Values],
                Issues: [.. this._issues.Values],
                PollingStates: new Dictionary<string, string>(this._pollingStates, StringComparer.Ordinal),
                PullRequestAssignees: ExportAssignees(this._pullRequestAssignees),
                IssueAssignees: ExportAssignees(this._issueAssignees),
                PullRequestCommitAuthors: ExportAssignees(this._pullRequestCommitAuthors)
            );
        }
    }

    // Replaces all store state from a previously exported snapshot. Only ever
    // safe to call once, at startup, before any other caller can observe or mutate the store -
    // see DispatcherStoreSnapshotLoader and the synchronous load ordering in ServerStartup.
    internal void ImportSnapshot(DispatcherStoreSnapshotData snapshot)
    {
        lock (this._gate)
        {
            ReplaceAll(target: this._repos, source: snapshot.Repos);
            ReplaceAll(
                target: this._pullRequests,
                source: snapshot.PullRequests,
                keySelector: static row => (row.Repository, row.Id)
            );
            ReplaceAll(
                target: this._issues,
                source: snapshot.Issues,
                keySelector: static row => (row.Repository, row.Id)
            );
            ReplaceAll(target: this._pollingStates, source: snapshot.PollingStates);
            ImportAssignees(target: this._pullRequestAssignees, source: snapshot.PullRequestAssignees);
            ImportAssignees(target: this._issueAssignees, source: snapshot.IssueAssignees);
            ImportAssignees(target: this._pullRequestCommitAuthors, source: snapshot.PullRequestCommitAuthors);
        }
    }

    private static AssigneeSnapshotRow[] ExportAssignees(Dictionary<(string Repository, int Id), string[]> source)
    {
        return
        [
            .. source.Select(static entry => new AssigneeSnapshotRow(entry.Key.Repository, entry.Key.Id, entry.Value)),
        ];
    }

    // Snapshots written before assignees (or commit authors) were tracked have no such arrays, so a null source
    // loads as "nothing assigned" rather than being rejected.
    private static void ImportAssignees(
        Dictionary<(string Repository, int Id), string[]> target,
        AssigneeSnapshotRow[]? source
    )
    {
        target.Clear();

        foreach (AssigneeSnapshotRow row in (source ?? []).Where(static row => row.Logins is { Length: > 0 }))
        {
            target[(row.Repository, row.Id)] = row.Logins;
        }
    }

    // A null list means the caller had no assignee information, so the stored set is left alone;
    // an empty list means the item is genuinely unassigned.
    private static void ReplaceAssignees(
        Dictionary<(string Repository, int Id), string[]> target,
        (string Repository, int Id) key,
        IReadOnlyList<string>? assignees
    )
    {
        if (assignees is null)
        {
            return;
        }

        string[] logins = [.. assignees.Distinct(StringComparer.OrdinalIgnoreCase)];

        if (logins.Length == 0)
        {
            target.Remove(key);

            return;
        }

        target[key] = logins;
    }

    private static bool IsVisibleTo(
        string? user,
        Dictionary<(string Repository, int Id), string[]> assigneesByItem,
        (string Repository, int Id) key
    )
    {
        return user is null
            || !assigneesByItem.TryGetValue(key: key, out string[]? assignees)
            || assignees.Contains(value: user, comparer: StringComparer.OrdinalIgnoreCase);
    }

    private static void ReplaceAll<TKey, TValue>(Dictionary<TKey, TValue> target, Dictionary<TKey, TValue> source)
        where TKey : notnull
    {
        target.Clear();

        foreach (KeyValuePair<TKey, TValue> entry in source)
        {
            target[entry.Key] = entry.Value;
        }
    }

    private static void ReplaceAll<TKey, TValue>(
        Dictionary<TKey, TValue> target,
        TValue[] source,
        Func<TValue, TKey> keySelector
    )
        where TKey : notnull
    {
        target.Clear();
        target.EnsureCapacity(source.Length);

        foreach (TValue value in source)
        {
            target[keySelector(value)] = value;
        }
    }

    private bool IsRepoExplicitlyInactiveNoLock(string repository)
    {
        return this._repos.TryGetValue(repository, out bool isActive) && !isActive;
    }

    private bool IsLinkedPullRequestActiveNoLock(string repository, int? linkedPrNumber)
    {
        return linkedPrNumber is { } prNumber
            && this._pullRequests.TryGetValue((repository, prNumber), out PullRequestRow? pr)
            && IsOpenOrDraft(pr.Status);
    }

    // A repository is busy only for an open/draft PR that is not on hold and, when a user is given,
    // that the user would be given (adopted, unassigned or assigned to them) and is committing to.
    // A PR assigned only to other people is ignored for that user, as it is in the active PR list.
    // A PR whose commit authors are unknown still counts, so missing data never lets concurrent work
    // into a repository.
    private bool HasActivePullRequestNoLock(string repository, string? user)
    {
        return this._pullRequests.Values.Any(pr =>
            string.Equals(pr.Repository, repository, StringComparison.Ordinal)
            && IsOpenOrDraft(pr.Status)
            && !pr.IsOnHold
            && (
                pr.IsAdopted
                || IsVisibleTo(user: user, assigneesByItem: this._pullRequestAssignees, key: (pr.Repository, pr.Id))
            )
            && this.IsCommittedToBy(user: user, key: (pr.Repository, pr.Id))
        );
    }

    private bool IsCommittedToBy(string? user, (string Repository, int Id) key)
    {
        return IsVisibleTo(user: user, assigneesByItem: this._pullRequestCommitAuthors, key: key);
    }

    private static bool IsOpenOrDraft(string status)
    {
        return string.Equals(status, OPEN_STATUS, StringComparison.Ordinal)
            || string.Equals(status, DRAFT_STATUS, StringComparison.Ordinal);
    }
}
