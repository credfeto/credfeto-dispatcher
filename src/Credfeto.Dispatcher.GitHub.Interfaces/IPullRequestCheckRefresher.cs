using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IPullRequestCheckRefresher
{
    // Returns the longest X-Poll-Interval GitHub asked for, or null when it asked for none.
    ValueTask<int?> RefreshAsync(CancellationToken cancellationToken);
}
