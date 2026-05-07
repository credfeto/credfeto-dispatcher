using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Server.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.Server;

internal static partial class Endpoints
{
    public static void MapWorkItemEndpoints(this WebApplication app)
    {
        app.MapGet(pattern: "/priorities", handler: GetPrioritiesAsync);
    }

    private static async Task<IResult> GetPrioritiesAsync(
        [FromServices] IWorkItemRepository workItemRepository,
        [FromServices] IOptions<PrioritiesOptions> options,
        CancellationToken cancellationToken
    )
    {
        PrioritiesOptions config = options.Value;
        IReadOnlyList<WorkItem> items = await workItemRepository.GetPrioritisedWorkItemsAsync(
            owners: config.Owners,
            repos: config.Repos,
            cancellationToken: cancellationToken
        );

        return Results.Ok(items);
    }
}
