using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Entities;

[DebuggerDisplay("{Repository}: IsActive={IsActive}")]
public sealed class RepoEntity
{
    public string Repository { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset LastUpdated { get; set; }
}
