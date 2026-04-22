using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface INotificationFilter
{
    bool ShouldDispatch(GitHubNotification notification);
}
