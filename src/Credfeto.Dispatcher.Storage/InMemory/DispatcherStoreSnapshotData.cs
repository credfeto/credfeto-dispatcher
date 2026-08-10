using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.Dispatcher.Storage.Database.Rows;

namespace Credfeto.Dispatcher.Storage.InMemory;

// The on-disk shape of an InMemoryDispatcherStore snapshot. Repos/PollingStates have string
// keys, so System.Text.Json source generation handles them natively; only the tuple-keyed
// PullRequests/Issues dictionaries need flattening to a row array to round-trip.
[DebuggerDisplay(
    "Repos={Repos.Count}, PullRequests={PullRequests.Length}, Issues={Issues.Length}, PollingStates={PollingStates.Count}"
)]
internal sealed record DispatcherStoreSnapshotData(
    Dictionary<string, bool> Repos,
    PullRequestRow[] PullRequests,
    IssueRow[] Issues,
    Dictionary<string, string> PollingStates
);
