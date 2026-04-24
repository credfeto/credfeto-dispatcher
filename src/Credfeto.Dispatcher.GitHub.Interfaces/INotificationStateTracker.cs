using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface INotificationStateTracker
{
    Task<bool> PullRequestExistsAsync(GitHubNotification notification, int number, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if a pull request notification should be skipped.
    /// Returns true if the PR is closed and has already been marked as closed, or if state hasn't changed.
    /// </summary>
    Task<bool> ShouldSkipPullRequestAsync(GitHubNotification notification, PullRequestDetails details, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the stored state of a pull request.
    /// </summary>
    Task UpdatePullRequestStateAsync(GitHubNotification notification, PullRequestDetails details, CancellationToken cancellationToken);

    Task<bool> IssueExistsAsync(GitHubNotification notification, int number, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if an issue notification should be skipped.
    /// Returns true if the issue is closed and has already been marked as closed, or if state hasn't changed.
    /// </summary>
    Task<bool> ShouldSkipIssueAsync(GitHubNotification notification, IssueDetails details, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the stored state of an issue.
    /// </summary>
    Task UpdateIssueStateAsync(GitHubNotification notification, IssueDetails details, CancellationToken cancellationToken);
}
