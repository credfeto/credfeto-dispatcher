using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface INotificationStateTracker
{
    Task<bool> ShouldSkipPullRequestAsync(GitHubNotification notification, PullRequestDetails details, CancellationToken cancellationToken);

    Task UpdatePullRequestStateAsync(GitHubNotification notification, PullRequestDetails details, CancellationToken cancellationToken);

    Task<bool> ShouldSkipIssueAsync(GitHubNotification notification, IssueDetails details, CancellationToken cancellationToken);

    Task UpdateIssueStateAsync(GitHubNotification notification, IssueDetails details, CancellationToken cancellationToken);
}
