using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface INotificationStateTracker
{
    Task<bool> ShouldSkipPullRequestAsync(
        string repository,
        int pullRequestNumber,
        string currentStatus,
        CancellationToken cancellationToken
    );

    Task UpdatePullRequestStateAsync(
        string repository,
        int pullRequestNumber,
        string status,
        WorkPriority priority,
        bool isOnHold,
        CancellationToken cancellationToken
    );

    Task<bool> ShouldSkipIssueAsync(
        string repository,
        int issueNumber,
        string currentStatus,
        CancellationToken cancellationToken
    );

    Task UpdateIssueStateAsync(
        string repository,
        int issueNumber,
        string status,
        WorkPriority priority,
        bool isOnHold,
        bool hasLinkedPr,
        CancellationToken cancellationToken
    );
}
