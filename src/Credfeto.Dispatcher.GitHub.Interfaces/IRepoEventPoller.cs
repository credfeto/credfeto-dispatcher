using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IRepoEventPoller
{
    ValueTask<int?> PollAsync(CancellationToken cancellationToken);
}
