using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
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
        [FromServices] IOptions<GitHubOptions> options,
        CancellationToken cancellationToken
    )
    {
        try
        {
            GitHubFilterOptions filter = options.Value.Filter;
            PrioritiesResponse response = await workItemRepository.GetPrioritisedWorkItemsAsync(
                owners: filter.AllowedOwners,
                maxIssues: filter.MaxIssues,
                cancellationToken: cancellationToken
            );

            string json = JsonSerializer.Serialize(response, AppJsonContexts.Default.PrioritiesResponse);

            return Results.Content(content: json, contentType: "application/json");
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.ToString(), statusCode: 500);
        }
    }
}
