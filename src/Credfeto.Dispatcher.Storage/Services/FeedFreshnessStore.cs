using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;

namespace Credfeto.Dispatcher.Storage.Services;

// Kept in the polling-state store, so it survives restarts with the SQL store and needs no schema change.
public sealed class FeedFreshnessStore : IFeedFreshnessStore
{
    internal const string LAST_SYNC_KEY = "freshness.lastsync";

    private readonly IETagStore _eTagStore;

    public FeedFreshnessStore(IETagStore eTagStore)
    {
        this._eTagStore = eTagStore;
    }

    public async ValueTask RecordSyncAsync(DateTimeOffset syncedAt, CancellationToken cancellationToken)
    {
        DateTimeOffset? stored = await this.GetLastSyncAsync(cancellationToken);

        if (stored is { } previous && previous >= syncedAt)
        {
            return;
        }

        await this._eTagStore.SaveETagAsync(
            key: LAST_SYNC_KEY,
            eTag: syncedAt.ToUniversalTime().ToString(format: "O", formatProvider: CultureInfo.InvariantCulture),
            cancellationToken: cancellationToken
        );
    }

    public async ValueTask<DateTimeOffset?> GetLastSyncAsync(CancellationToken cancellationToken)
    {
        string? value = await this._eTagStore.GetETagAsync(key: LAST_SYNC_KEY, cancellationToken: cancellationToken);

        return DateTimeOffset.TryParse(
            input: value,
            formatProvider: CultureInfo.InvariantCulture,
            styles: DateTimeStyles.RoundtripKind,
            result: out DateTimeOffset parsed
        )
            ? parsed
            : null;
    }
}
