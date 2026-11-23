using System;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IFeedFreshnessStore
{
    // Records that every tracked source was confirmed in sync as of syncedAt (a complete event-poll cycle or full
    // scan). An older time than the one stored is ignored.
    ValueTask RecordSyncAsync(DateTimeOffset syncedAt, CancellationToken cancellationToken);

    ValueTask<DateTimeOffset?> GetLastSyncAsync(CancellationToken cancellationToken);
}
