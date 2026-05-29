using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface INotificationFilter
{
    bool ShouldProcess(GitHubNotification notification);
}
