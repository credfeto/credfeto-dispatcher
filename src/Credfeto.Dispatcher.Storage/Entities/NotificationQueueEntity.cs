using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Entities;

[DebuggerDisplay("{Repository}/{SubjectType}: {SubjectTitle}")]
public sealed class NotificationQueueEntity
{
    public string SubjectUrl { get; set; } = string.Empty;

    public string NotificationId { get; set; } = string.Empty;

    public string Repository { get; set; } = string.Empty;

    public string RepositoryUrl { get; set; } = string.Empty;

    public string SubjectType { get; set; } = string.Empty;

    public string SubjectTitle { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset QueuedAt { get; set; }

    public DateTimeOffset DispatchAfter { get; set; }
}
