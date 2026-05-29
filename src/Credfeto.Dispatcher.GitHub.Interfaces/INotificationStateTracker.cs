using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface INotificationStateTracker
{
    ValueTask UpdateStateAsync(
        GitHubNotification notification,
        PullRequestDetails details,
        WorkPriority priority,
        bool isOnHold,
        CancellationToken cancellationToken
    );

    ValueTask UpdateStateAsync(
        GitHubNotification notification,
        IssueDetails details,
        WorkPriority priority,
        bool isOnHold,
        CancellationToken cancellationToken
    );
}
