using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IActiveRepoTracker
{
    ValueTask<IReadOnlyList<string>> GetActiveReposAsync(CancellationToken cancellationToken);

    ValueTask UpdateActiveReposAsync(IReadOnlyList<string> activeRepos, CancellationToken cancellationToken);
}
