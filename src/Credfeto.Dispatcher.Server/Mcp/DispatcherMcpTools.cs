using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Credfeto.Dispatcher.Server.Mcp;

[McpServerToolType]
[SuppressMessage(
    category: "Microsoft.Performance",
    checkId: "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Instantiated by the MCP SDK via WithTools"
)]
internal sealed class DispatcherMcpTools
{
    private readonly GitHubOptions _options;
    private readonly IWorkItemRepository _workItemRepository;

    public DispatcherMcpTools(IWorkItemRepository workItemRepository, IOptions<GitHubOptions> options)
    {
        this._workItemRepository = workItemRepository;
        this._options = options.Value;
    }

    [McpServerTool(Name = "priorities", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Returns the prioritised list of open pull requests and issues, the same data as the /priorities endpoint."
    )]
    public async Task<string> GetPrioritiesAsync(
        [Description("Optional GitHub login; only items assigned to this user, or unassigned, are returned.")]
            string? user = null,
        CancellationToken cancellationToken = default
    )
    {
        string? userFilter = ValidateFilter(value: user, parameterName: "user", isValid: GitHubLoginValidator.IsValid);

        GitHubFilterOptions filter = this._options.Filter;

        PrioritiesResponse response = await this._workItemRepository.GetPrioritisedWorkItemsAsync(
            owners: filter.AllowedOwners,
            maxIssues: filter.MaxIssues,
            boostedRepos: filter.BoostedRepos,
            user: userFilter,
            cancellationToken: cancellationToken
        );

        return JsonSerializer.Serialize(value: response, jsonTypeInfo: AppJsonContexts.Default.PrioritiesResponse);
    }

    [McpServerTool(Name = "db", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Returns every pull request and issue the dispatcher has stored, including closed and on-hold items, without paging."
    )]
    public async Task<string> GetDatabaseStateAsync(
        [Description("Optional repository owner (the part before '/'); exact, case-insensitive match.")]
            string? owner = null,
        [Description("Optional repository name (the part after '/'); exact, case-insensitive match.")]
            string? repo = null,
        [Description("Optional GitHub login; only items assigned to this user, or unassigned, are returned.")]
            string? user = null,
        CancellationToken cancellationToken = default
    )
    {
        string? ownerFilter = ValidateFilter(
            value: owner,
            parameterName: "owner",
            isValid: GitHubLoginValidator.IsValid
        );
        string? repoFilter = ValidateFilter(
            value: repo,
            parameterName: "repo",
            isValid: GitHubRepositoryNameValidator.IsValid
        );
        string? userFilter = ValidateFilter(value: user, parameterName: "user", isValid: GitHubLoginValidator.IsValid);

        DbStateResponse response = await this._workItemRepository.GetDatabaseStateAsync(
            owner: ownerFilter,
            repo: repoFilter,
            user: userFilter,
            cancellationToken: cancellationToken
        );

        return JsonSerializer.Serialize(value: response, jsonTypeInfo: AppJsonContexts.Default.DbStateResponse);
    }

    private static string? ValidateFilter(string? value, string parameterName, Func<string, bool> isValid)
    {
        string? filter = WorkItemQueries.NormaliseFilter(value);

        if (filter is not null && !isValid(filter))
        {
            throw new McpException($"Invalid {parameterName}");
        }

        return filter;
    }
}
