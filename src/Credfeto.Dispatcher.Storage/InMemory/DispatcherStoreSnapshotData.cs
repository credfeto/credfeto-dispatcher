using System.Diagnostics;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage.InMemory;

// The on-disk shape of an InMemoryDispatcherStore snapshot: every tuple-keyed dictionary
// flattened to a row array so it round-trips through System.Text.Json source generation.
[DebuggerDisplay(
    "Repos={Repos.Length}, PullRequests={PullRequests.Length}, Issues={Issues.Length}, PollingStates={PollingStates.Length}"
)]
internal sealed record DispatcherStoreSnapshotData(
    RepoEntry[] Repos,
    PullRequestRow[] PullRequests,
    IssueRow[] Issues,
    PollingStateEntry[] PollingStates
);
