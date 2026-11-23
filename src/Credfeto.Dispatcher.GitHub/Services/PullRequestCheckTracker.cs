using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Services;

// In-process CI state of the open PRs the dispatcher serves, so the poll cycle can re-read the checks of only the
// PRs whose CI is unknown, pending or failed. It is seeded by the full scan, PR events and every detail fetch, and
// rebuilt by the first scan after a restart; nothing here is persisted.
public sealed class PullRequestCheckTracker
{
    private const string CLOSED_STATUS = "Closed";

    private readonly Lock _gate = new();
    private readonly Dictionary<string, PullRequestCheckState> _states = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly TimeProvider _timeProvider;

    public PullRequestCheckTracker(TimeProvider timeProvider)
    {
        this._timeProvider = timeProvider;
    }

    public int Count
    {
        get
        {
            lock (this._gate)
            {
                return this._states.Count;
            }
        }
    }

    public PullRequestCheckState? Get(string repo, int number)
    {
        lock (this._gate)
        {
            return this._states.GetValueOrDefault(BuildKey(repo: repo, number: number));
        }
    }

    // A sighting without check detail (scan or PR event). A new PR, or a new head commit, makes the PR a candidate
    // ahead of everything already checked.
    public void Observe(string repo, int number, string? headSha, bool isOnHold, bool isOpen)
    {
        string key = BuildKey(repo: repo, number: number);

        lock (this._gate)
        {
            if (!isOpen)
            {
                this._states.Remove(key);

                return;
            }

            this._states[key] =
                this._states.TryGetValue(key, out PullRequestCheckState? existing)
                && !IsNewHead(existing: existing, headSha: headSha)
                    ? existing with
                    {
                        HeadSha = existing.HeadSha ?? headSha,
                        IsOnHold = isOnHold,
                    }
                    : Unchecked(repo: repo, number: number, headSha: headSha, isOnHold: isOnHold);
        }
    }

    // A full detail fetch: the runs read with it become the known CI state.
    public void RecordDetail(string repo, int number, PullRequestDetails details, bool isOnHold)
    {
        string key = BuildKey(repo: repo, number: number);

        if (StringComparer.OrdinalIgnoreCase.Equals(details.Status, CLOSED_STATUS))
        {
            lock (this._gate)
            {
                this._states.Remove(key);
            }

            return;
        }

        (string signature, bool settled) = CheckRunSignature.Compute([
            .. details.Runs.Select(static r => (r.Name, r.Status, r.Conclusion)),
        ]);
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        lock (this._gate)
        {
            PullRequestCheckState state = this._states.TryGetValue(key, out PullRequestCheckState? existing)
                ? existing
                : Unchecked(repo: repo, number: number, headSha: details.HeadSha, isOnHold: isOnHold);

            this._states[key] = state with
            {
                HeadSha = details.HeadSha ?? state.HeadSha,
                ETag = IsNewHead(existing: state, headSha: details.HeadSha) ? null : state.ETag,
                RunSignature = signature,
                Settled = settled,
                IsOnHold = isOnHold,
                LastChecked = now,
            };
        }
    }

    // A runs request answered for the head commit the candidate was selected with. Ignored when the head has moved
    // on since, because the ETag and the check time belong to the old head.
    public void RecordChecked(PullRequestCheckState checkedState, string? eTag)
    {
        string key = BuildKey(repo: checkedState.Repo, number: checkedState.Number);
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        lock (this._gate)
        {
            if (
                this._states.TryGetValue(key, out PullRequestCheckState? existing)
                && StringComparer.OrdinalIgnoreCase.Equals(existing.HeadSha, checkedState.HeadSha)
            )
            {
                this._states[key] = existing with { ETag = eTag ?? existing.ETag, LastChecked = now };
            }
        }
    }

    // An item whose refresh was deferred: check it again ahead of everything else.
    public void MarkStale(string repo, int number)
    {
        string key = BuildKey(repo: repo, number: number);

        lock (this._gate)
        {
            this._states[key] = this._states.TryGetValue(key, out PullRequestCheckState? existing)
                ? existing with
                {
                    Settled = false,
                    LastChecked = DateTimeOffset.MinValue,
                }
                : Unchecked(repo: repo, number: number, headSha: null, isOnHold: false);
        }
    }

    public void Remove(string repo, int number)
    {
        lock (this._gate)
        {
            this._states.Remove(BuildKey(repo: repo, number: number));
        }
    }

    // After a complete listing of a repo's open PRs, forget the ones that are no longer open.
    public void RetainOnly(string repo, IReadOnlyCollection<int> openNumbers)
    {
        HashSet<int> open = [.. openNumbers];

        lock (this._gate)
        {
            string[] closed =
            [
                .. this
                    ._states.Where(entry =>
                        StringComparer.OrdinalIgnoreCase.Equals(entry.Value.Repo, repo)
                        && !open.Contains(entry.Value.Number)
                    )
                    .Select(static entry => entry.Key),
            ];

            foreach (string key in closed)
            {
                this._states.Remove(key);
            }
        }
    }

    // Least recently checked first, so a bounded batch per cycle works through every candidate in turn.
    public IReadOnlyList<PullRequestCheckState> SelectCandidates(int maxCount)
    {
        if (maxCount <= 0)
        {
            return [];
        }

        lock (this._gate)
        {
            return
            [
                .. this
                    ._states.Values.Where(static s => !s.IsOnHold && !s.Settled)
                    .OrderBy(static s => s.LastChecked)
                    .ThenBy(static s => s.Repo, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static s => s.Number)
                    .Take(maxCount),
            ];
        }
    }

    private static bool IsNewHead(PullRequestCheckState existing, string? headSha)
    {
        return headSha is not null
            && existing.HeadSha is not null
            && !StringComparer.OrdinalIgnoreCase.Equals(existing.HeadSha, headSha);
    }

    private static PullRequestCheckState Unchecked(string repo, int number, string? headSha, bool isOnHold)
    {
        return new(
            Repo: repo,
            Number: number,
            HeadSha: headSha,
            ETag: null,
            RunSignature: null,
            Settled: false,
            IsOnHold: isOnHold,
            LastChecked: DateTimeOffset.MinValue
        );
    }

    private static string BuildKey(string repo, int number)
    {
        return string.Concat(repo, "#", number.ToString(CultureInfo.InvariantCulture));
    }
}
