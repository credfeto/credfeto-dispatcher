using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IWorkItemRepository
{
    Task<PrioritiesResponse> GetPrioritisedWorkItemsAsync(
        IReadOnlyList<string> owners,
        IReadOnlyList<string> repos,
        TimeSpan stuckDependabotTimeout,
        IReadOnlyList<BotPrRule> additionalBotPrRules,
        int maxIssues,
        CancellationToken cancellationToken
    );

    ValueTask RemoveItemsForRepositoriesAsync(IReadOnlyList<string> repositories, CancellationToken cancellationToken);

    ValueTask CloseStaleItemsForRepoAsync(
        string repository,
        IReadOnlyList<int> activePullRequestNumbers,
        IReadOnlyList<int> activeIssueNumbers,
        CancellationToken cancellationToken
    );
}
