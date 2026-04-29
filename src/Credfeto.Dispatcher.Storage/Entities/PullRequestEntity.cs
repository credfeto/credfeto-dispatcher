using System;
using System.Diagnostics;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.Storage.Entities;

[DebuggerDisplay("{Repository}#{Id}: {Status}")]
public sealed class PullRequestEntity : INotificationEntity
{
    public string Repository { get; set; } = string.Empty;

    public int Id { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset FirstSeen { get; set; }

    public DateTimeOffset LastUpdated { get; set; }

    public DateTimeOffset? WhenClosed { get; set; }

    public WorkPriority Priority { get; set; } = WorkPriority.Unknown;

    public bool IsOnHold { get; set; }
}
