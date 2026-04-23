using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Id}: {Timestamp}")]
public sealed record LastNotification(string Id, DateTimeOffset Timestamp)
{
    public static LastNotification FromNotification(GitHubNotification notification)
    {
        return new LastNotification(Id: notification.Id, Timestamp: notification.UpdatedAt);
    }
}
