using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IIssueDetailFetcher
{
    ValueTask<IssueDetails?> FetchAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    );
}
