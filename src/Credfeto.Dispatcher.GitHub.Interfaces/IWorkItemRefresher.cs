using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IWorkItemRefresher
{
    ValueTask<bool> RefreshPullRequestAsync(GitHubNotification notification, CancellationToken cancellationToken);

    ValueTask<bool> RefreshPullRequestAsync(string repo, int number, CancellationToken cancellationToken);

    ValueTask<bool> RefreshIssueAsync(GitHubNotification notification, CancellationToken cancellationToken);
}
