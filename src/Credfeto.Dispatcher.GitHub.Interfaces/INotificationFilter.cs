using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface INotificationFilter
{
    bool ShouldDispatch(GitHubNotification notification);

    /// <summary>
    /// Returns true when the notification is from an owner/repo that should be tracked for state changes,
    /// even if the notification reason does not qualify it for a Discord dispatch.
    /// Used to detect closures arriving with reason "subscribed" and similar.
    /// </summary>
    bool ShouldTrackState(GitHubNotification notification);
}
