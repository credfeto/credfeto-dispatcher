using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface INotificationStateTracker
{
    ValueTask<bool> ShouldSkipAsync(
        GitHubNotification notification,
        PullRequestDetails details,
        CancellationToken cancellationToken
    );

    ValueTask UpdateStateAsync(
        GitHubNotification notification,
        PullRequestDetails details,
        WorkPriority priority,
        bool isOnHold,
        CancellationToken cancellationToken
    );

    ValueTask<bool> ShouldSkipAsync(
        GitHubNotification notification,
        IssueDetails details,
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
