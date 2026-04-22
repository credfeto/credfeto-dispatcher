using System;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

public sealed record GitHubNotification(
    string Id,
    string Reason,
    NotificationSubject Subject,
    NotificationRepository Repository,
    DateTimeOffset UpdatedAt,
    bool Unread
);
