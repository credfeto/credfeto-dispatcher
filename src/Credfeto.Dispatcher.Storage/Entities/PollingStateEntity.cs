using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Entities;

[DebuggerDisplay("{Key}: {ETag}")]
public sealed class PollingStateEntity
{
    [Key]
    [MaxLength(256)]
    public required string Key { get; init; }

    [MaxLength(1024)]
    public required string ETag { get; init; }
}
