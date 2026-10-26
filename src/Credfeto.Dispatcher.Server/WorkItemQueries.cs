using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;

namespace Credfeto.Dispatcher.Server;

internal static class WorkItemQueries
{
    public static string? NormaliseFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static Task<PrioritiesResponse> GetPrioritiesAsync(
        IWorkItemRepository workItemRepository,
        GitHubFilterOptions filter,
        string? user,
        in CancellationToken cancellationToken
    )
    {
        return workItemRepository.GetPrioritisedWorkItemsAsync(
            owners: filter.AllowedOwners,
            maxIssues: filter.MaxIssues,
            boostedRepos: filter.BoostedRepos,
            user: user,
            cancellationToken: cancellationToken
        );
    }
}
