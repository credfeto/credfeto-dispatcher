using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Entities;

[DebuggerDisplay("{Key}: {ETag}")]
public sealed class PollingStateEntity
{
    [Key]
    public required string Key { get; init; }

    public required string ETag { get; init; }
}
