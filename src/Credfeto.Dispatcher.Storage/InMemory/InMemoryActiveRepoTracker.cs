using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Interfaces;

namespace Credfeto.Dispatcher.Storage.InMemory;

public sealed class InMemoryActiveRepoTracker : IActiveRepoTracker
{
    private readonly InMemoryDispatcherStore _store;

    public InMemoryActiveRepoTracker(InMemoryDispatcherStore store)
    {
        this._store = store;
    }

    public ValueTask<IReadOnlyList<string>> GetActiveReposAsync(CancellationToken cancellationToken)
    {
        return new ValueTask<IReadOnlyList<string>>(this._store.GetActiveRepos());
    }

    public ValueTask UpdateActiveReposAsync(IReadOnlyList<string> activeRepos, CancellationToken cancellationToken)
    {
        this._store.SetActiveRepos(activeRepos);

        return ValueTask.CompletedTask;
    }
}
