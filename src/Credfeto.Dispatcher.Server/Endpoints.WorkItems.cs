using System;
using System.Text.Json;
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
        try
        {
            PrioritiesOptions config = options.Value;
            PrioritiesResponse response = await workItemRepository.GetPrioritisedWorkItemsAsync(
                owners: config.Owners,
                repos: config.Repos,
                stuckDependabotTimeout: TimeSpan.FromHours(config.StuckDependabotTimeoutHours),
                maxIssues: config.MaxIssues,
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
