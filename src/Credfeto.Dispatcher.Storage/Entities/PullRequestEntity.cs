using System;
using System.Diagnostics;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.Storage.Entities;

[DebuggerDisplay("{Repository}#{Id}: {Status}")]
public sealed class PullRequestEntity : INotificationEntity
{
    public string Repository { get; set; } = string.Empty;

    public int Id { get; set; }

    public WorkItemStatus Status { get; set; } = WorkItemStatus.Open;

    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Unknown;

    public bool OnHold { get; set; }

    public string? State { get; set; }

    public DateTimeOffset FirstSeen { get; set; }

    public DateTimeOffset LastUpdated { get; set; }

    public DateTimeOffset? WhenClosed { get; set; }
}
