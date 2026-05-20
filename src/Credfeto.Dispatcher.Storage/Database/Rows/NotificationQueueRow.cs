using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Storage.Database.Rows;

[DebuggerDisplay("{Repository}: {NotificationId}")]
internal sealed record NotificationQueueRow(
    string SubjectUrl,
    string NotificationId,
    string Repository,
    string RepositoryUrl,
    string SubjectType,
    string SubjectTitle,
    string Reason,
    DateTimeOffset UpdatedAt,
    DateTimeOffset QueuedAt,
    DateTimeOffset DispatchAfter
);
