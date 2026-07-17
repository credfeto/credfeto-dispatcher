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
    private readonly Dictionary<string, string> _pollingStates = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public InMemoryDispatcherStore(TimeProvider timeProvider)
    {
        this._timeProvider = timeProvider;
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
            this._pollingStates[key] = eTag;
        }
    }

    public void UpsertPullRequest(
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
        string? author
    )
    {
        lock (this._gate)
        {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            bool isClosed = string.Equals(status, CLOSED_STATUS, StringComparison.Ordinal);
            (string Repository, int Id) key = (repository, id);

            if (this._pullRequests.TryGetValue(key, out PullRequestRow? existing))
            {
                this._pullRequests[key] = existing with
                {
                    Status = status,
                    Priority = priority,
                    IsOnHold = isOnHold,
                    CommentCount = commentCount,
                    ReviewDecision = reviewDecision,
                    FailedCheckCount = failedCheckCount,
                    FailedCheckNames = failedCheckNames,
                    FailedCheckSha = failedCheckSha,
                    Author = author ?? existing.Author,
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
                Author: author
            );
        }
    }

    public void UpsertIssue(string repository, int id, string status, int priority, bool isOnHold, int? linkedPrNumber)
    {
        lock (this._gate)
        {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            bool isClosed = string.Equals(status, CLOSED_STATUS, StringComparison.Ordinal);
            (string Repository, int Id) key = (repository, id);

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
                this._issues[key] = existing with
                {
                    LinkedPrNumber = linkedPrNumber,
                    LastUpdated = this._timeProvider.GetUtcNow(),
                };
            }
        }
    }

    internal (IReadOnlyList<PullRequestRow> PullRequests, IReadOnlyList<IssueRow> Issues) GetActiveWorkItems()
    {
        lock (this._gate)
        {
            IReadOnlyList<PullRequestRow> activePullRequests =
            [
                .. this._pullRequests.Values.Where(pr =>
                    IsOpenOrDraft(pr.Status) && !pr.IsOnHold && !this.IsRepoExplicitlyInactiveNoLock(pr.Repository)
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
                        issue.Priority >= (int)WorkPriority.URGENT || !this.HasActivePullRequestNoLock(issue.Repository)
                    )
                ),
            ];

            return (activePullRequests, activeIssues);
        }
    }

    public void CloseStalePullRequests(string repository, IReadOnlyCollection<int>? activeIds)
    {
        lock (this._gate)
        {
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
            HashSet<string> repos = new(repositories, StringComparer.Ordinal);

            foreach (
                (string Repository, int Id) key in this
                    ._pullRequests.Keys.Where(k => repos.Contains(k.Repository))
                    .ToArray()
            )
            {
                this._pullRequests.Remove(key);
            }

            foreach (
                (string Repository, int Id) key in this._issues.Keys.Where(k => repos.Contains(k.Repository)).ToArray()
            )
            {
                this._issues.Remove(key);
            }
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

    private bool HasActivePullRequestNoLock(string repository)
    {
        return this._pullRequests.Values.Any(pr =>
            string.Equals(pr.Repository, repository, StringComparison.Ordinal) && IsOpenOrDraft(pr.Status)
        );
    }

    private static bool IsOpenOrDraft(string status)
    {
        return string.Equals(status, OPEN_STATUS, StringComparison.Ordinal)
            || string.Equals(status, DRAFT_STATUS, StringComparison.Ordinal);
    }
}
