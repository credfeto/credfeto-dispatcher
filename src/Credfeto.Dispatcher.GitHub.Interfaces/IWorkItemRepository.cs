using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IWorkItemRepository
{
    Task<IReadOnlyList<WorkItem>> GetPrioritisedWorkItemsAsync(
        IReadOnlyList<string> owners,
        IReadOnlyList<string> repos,
        CancellationToken cancellationToken
    );
}
