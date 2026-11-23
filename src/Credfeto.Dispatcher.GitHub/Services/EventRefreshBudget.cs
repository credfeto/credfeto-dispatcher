using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.Services;

// The GitHub calls one event-poll cycle may spend re-reading the items its events touched
// (Filter:MaxEventRefreshesPerPoll). Pushed-branch lookups, PR re-reads and issue re-reads all draw on it; anything
// past it is counted as deferred.
[DebuggerDisplay(
    "Limit: {_limit}, Lookups: {LookupCount}, PRs: {PullRequestCount}, Issues: {IssueCount}, Deferred: {DeferredCount}"
)]
internal sealed class EventRefreshBudget
{
    private readonly int _limit;
    private int _issueCount;
    private int _lookupCount;
    private int _pullRequestCount;

    public EventRefreshBudget(int limit)
    {
        this._limit = limit;
    }

    public int LookupCount => this._lookupCount;

    public int PullRequestCount => this._pullRequestCount;

    public int IssueCount => this._issueCount;

    public int DeferredCount { get; private set; }

    public bool IsUnused => this._lookupCount + this._pullRequestCount + this._issueCount + this.DeferredCount == 0;

    public bool TrySpendLookup()
    {
        return this.TrySpend(ref this._lookupCount);
    }

    public bool TrySpendPullRequest()
    {
        return this.TrySpend(ref this._pullRequestCount);
    }

    public bool TrySpendIssue()
    {
        return this.TrySpend(ref this._issueCount);
    }

    private bool TrySpend(ref int counter)
    {
        if (this._lookupCount + this._pullRequestCount + this._issueCount >= this._limit)
        {
            ++this.DeferredCount;

            return false;
        }

        ++counter;

        return true;
    }
}
