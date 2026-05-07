using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Credfeto.Dispatcher.Storage;

public sealed class WorkItemRepository : IWorkItemRepository
{
    private const string PullRequestType = "PullRequest";
    private const string IssueType = "Issue";
    private const string ClosedStatus = "Closed";

    private readonly IDbContextFactory<DispatcherDbContext> _dbContextFactory;

    public WorkItemRepository(IDbContextFactory<DispatcherDbContext> dbContextFactory)
    {
        this._dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<WorkItem>> GetPrioritisedWorkItemsAsync(
        IReadOnlyList<string> owners,
        IReadOnlyList<string> repos,
        CancellationToken cancellationToken
    )
    {
        await using DispatcherDbContext context = await this._dbContextFactory.CreateDbContextAsync(
            cancellationToken
        );

        List<WorkItem> pullRequests = await context
            .PullRequests.Where(e => e.Status != ClosedStatus && !e.IsOnHold)
            .Select(e => new WorkItem(e.Repository, e.Id, PullRequestType, e.Priority, e.FirstSeen))
            .ToListAsync(cancellationToken);

        List<WorkItem> issues = await context
            .Issues.Where(e => e.Status != ClosedStatus && !e.IsOnHold && !e.HasLinkedPr)
            .Select(e => new WorkItem(e.Repository, e.Id, IssueType, e.Priority, e.FirstSeen))
            .ToListAsync(cancellationToken);

        List<WorkItem> combined = [.. pullRequests, .. issues];

        return
        [
            .. combined
                .OrderBy(w => FindIndex(owners, GetOwner(w.Repository)))
                .ThenBy(w => FindIndex(repos, w.Repository))
                .ThenBy(w => IsPullRequest(w) ? 0 : 1)
                .ThenByDescending(w => (int)w.Priority)
                .ThenBy(w => w.FirstSeen),
        ];
    }

    private static bool IsPullRequest(WorkItem w)
    {
        return string.Equals(
            a: w.ItemType,
            b: PullRequestType,
            comparisonType: StringComparison.Ordinal
        );
    }

    private static int FindIndex(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (
                string.Equals(
                    a: list[i],
                    b: value,
                    comparisonType: StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static string GetOwner(string repository)
    {
        int slash = repository.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);

        return slash >= 0 ? repository[..slash] : repository;
    }
}
