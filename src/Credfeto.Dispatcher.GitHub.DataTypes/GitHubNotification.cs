using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Id}: {Reason} - {Subject.Title}")]
public sealed record GitHubNotification(
    string Id,
    string Reason,
    NotificationSubject Subject,
    NotificationRepository Repository,
    DateTimeOffset UpdatedAt,
    bool Unread
);
