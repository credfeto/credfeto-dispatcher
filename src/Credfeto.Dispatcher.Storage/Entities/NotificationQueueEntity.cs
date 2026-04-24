using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Entities;

[DebuggerDisplay("{Repository}/{SubjectType}: {SubjectTitle}")]
public sealed class NotificationQueueEntity
{
    public Uri SubjectUrl { get; set; } = new(uriString: "about:blank");

    public string NotificationId { get; set; } = string.Empty;

    public string Repository { get; set; } = string.Empty;

    public Uri RepositoryUrl { get; set; } = new(uriString: "about:blank");

    public string SubjectType { get; set; } = string.Empty;

    public string SubjectTitle { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset QueuedAt { get; set; }

    public DateTimeOffset DispatchAfter { get; set; }
}
