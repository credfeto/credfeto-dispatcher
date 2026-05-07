using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IPullRequestDetailFetcher
{
    ValueTask<PullRequestDetails?> FetchAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    );
}
