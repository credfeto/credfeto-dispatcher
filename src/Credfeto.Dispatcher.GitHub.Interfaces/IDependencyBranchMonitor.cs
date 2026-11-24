using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IDependencyBranchMonitor
{
    // Applies one event-poll cycle's branch creations and deletions (oldest first), re-checks a bounded batch of
    // branches still without a pull request, and prunes settled rows. Report only: it never alerts or acts on a branch.
    ValueTask UpdateAsync(IReadOnlyList<DependencyBranchChange> changes, CancellationToken cancellationToken);
}
