using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Server.LoggingExtensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.Server;

internal static partial class Endpoints
{
    private const string ENDPOINTS_LOG_CATEGORY = "Credfeto.Dispatcher.Server.Endpoints";

    public static void MapWorkItemEndpoints(this WebApplication app)
    {
        app.MapGet(pattern: "/priorities", handler: GetPrioritiesAsync);
    }

    private static async Task<IResult> GetPrioritiesAsync(
        [FromServices] IWorkItemRepository workItemRepository,
        [FromServices] IOptions<GitHubOptions> options,
        [FromServices] ILoggerFactory loggerFactory,
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ILogger logger = loggerFactory.CreateLogger(ENDPOINTS_LOG_CATEGORY);
            logger.LogUnhandledException(ex);

            return Results.Problem(statusCode: 500);
        }
    }
}
