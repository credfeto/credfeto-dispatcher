using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Models;
using Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class WorkItemScanner : IWorkItemScanner
{
    private readonly IActiveRepoTracker _activeRepoTracker;
    private readonly GitHubRepoHelper _helper;
    private readonly ILogger<WorkItemScanner> _logger;
    private readonly INotificationStateTracker _notificationStateTracker;
    private readonly GitHubOptions _options;
    private readonly IWorkItemRepository _workItemRepository;

    public WorkItemScanner(
        GitHubRepoHelper helper,
        IActiveRepoTracker activeRepoTracker,
        INotificationStateTracker notificationStateTracker,
        IWorkItemRepository workItemRepository,
        IOptions<GitHubOptions> options,
        ILogger<WorkItemScanner> logger
    )
    {
        this._helper = helper;
        this._activeRepoTracker = activeRepoTracker;
        this._notificationStateTracker = notificationStateTracker;
        this._workItemRepository = workItemRepository;
        this._options = options.Value;
        this._logger = logger;
    }

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        (bool discoveryComplete, IReadOnlyList<string> repos, IReadOnlyList<string> inactiveRepos) =
            await this._helper.DiscoverReposAsync(
                shouldInclude: this.ShouldIncludeRepo,
                cancellationToken: cancellationToken
            );

        if (inactiveRepos.Count > 0)
        {
            this._logger.LogRemovingItemsForInactiveRepos(count: inactiveRepos.Count);
            await this._workItemRepository.RemoveItemsForRepositoriesAsync(
                repositories: inactiveRepos,
                cancellationToken: cancellationToken
            );
        }

        if (!discoveryComplete || repos.Count == 0)
        {
            this._logger.LogNoReposDiscovered();

            return;
        }

        await this._activeRepoTracker.UpdateActiveReposAsync(activeRepos: repos, cancellationToken: cancellationToken);

        this._logger.LogDiscoveredRepos(count: repos.Count);

        foreach (string repo in repos)
        {
            await this.ScanRepoAsync(repo: repo, cancellationToken: cancellationToken);
        }

        this._logger.LogScanComplete();
    }

    private bool ShouldIncludeRepo(ApiUserRepo repo)
    {
        if (repo.Archived || repo.Disabled)
        {
            this._logger.LogRepoSkippedInactive(repo.FullName);

            return false;
        }

        if (repo.Permissions?.Push != true)
        {
            this._logger.LogRepoSkippedNoPushPermission(repo.FullName);

            return false;
        }

        return this.PassesRepoFilter(repo.FullName);
    }

    private bool PassesRepoFilter(string fullName)
    {
        if (this._options.Filter.AllowedOwners.Count > 0)
        {
            string owner = GetOwner(fullName);

            if (
                !this._options.Filter.AllowedOwners.Any(o =>
                    string.Equals(a: o, b: owner, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                this._logger.LogRepoSkippedOwnerFilter(repo: fullName, owner: owner);

                return false;
            }
        }

        if (this._options.Filter.AllowedRepos.Count > 0)
        {
            if (
                !this._options.Filter.AllowedRepos.Any(r =>
                    string.Equals(a: r, b: fullName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                this._logger.LogRepoSkippedAllowedRepoFilter(repo: fullName);

                return false;
            }
        }

        if (this._options.Filter.ExcludedRepos.Count > 0)
        {
            if (
                this._options.Filter.ExcludedRepos.Any(r =>
                    string.Equals(a: r, b: fullName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                this._logger.LogRepoSkippedExcludedRepoFilter(repo: fullName);

                return false;
            }
        }

        return true;
    }

    private static string GetOwner(string fullName)
    {
        int slash = fullName.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);

        return slash >= 0 ? fullName[..slash] : fullName;
    }

    private async Task ScanRepoAsync(string repo, CancellationToken cancellationToken)
    {
        this._logger.LogScanningRepo(repo: repo);

        IReadOnlyList<int>? activePrNumbers = await this.ScanPullRequestsAsync(
            repo: repo,
            cancellationToken: cancellationToken
        );
        IReadOnlyList<int>? activeIssueNumbers = await this.ScanIssuesAsync(
            repo: repo,
            cancellationToken: cancellationToken
        );

        if (activePrNumbers is not null && activeIssueNumbers is not null)
        {
            await this._workItemRepository.CloseStaleItemsForRepoAsync(
                repository: repo,
                activePullRequestNumbers: activePrNumbers,
                activeIssueNumbers: activeIssueNumbers,
                cancellationToken: cancellationToken
            );
        }
        else
        {
            this._logger.LogRepoScanFailed(repo: repo);
            await this._workItemRepository.RemoveItemsForRepositoriesAsync(
                repositories: [repo],
                cancellationToken: cancellationToken
            );
        }
    }

    private async Task<IReadOnlyList<int>?> ScanPullRequestsAsync(string repo, CancellationToken cancellationToken)
    {
        string? url = $"repos/{repo}/pulls?state=open&per_page=100";
        List<int> activePrNumbers = [];

        while (url is not null)
        {
            (ApiPullRequest[]? items, string? nextUrl) = await this._helper.GetPagedAsync(
                url: url,
                jsonTypeInfo: NotificationSerializerContext.Default.ApiPullRequestArray,
                cancellationToken: cancellationToken
            );

            if (items is null)
            {
                return null;
            }

            foreach (ApiPullRequest pr in items)
            {
                await this.ProcessScannedPullRequestAsync(
                    pr: pr,
                    repo: repo,
                    activePrNumbers: activePrNumbers,
                    cancellationToken: cancellationToken
                );
            }

            url = nextUrl;
        }

        return activePrNumbers;
    }

    private async Task<IReadOnlyList<int>?> ScanIssuesAsync(string repo, CancellationToken cancellationToken)
    {
        string? url = $"repos/{repo}/issues?state=open&per_page=100";
        List<int> activeIssueNumbers = [];

        while (url is not null)
        {
            (ApiIssue[]? items, string? nextUrl) = await this._helper.GetPagedAsync(
                url: url,
                jsonTypeInfo: NotificationSerializerContext.Default.ApiIssueArray,
                cancellationToken: cancellationToken
            );

            if (items is null)
            {
                return null;
            }

            foreach (ApiIssue issue in items)
            {
                if (issue.PullRequest is not null)
                {
                    continue;
                }

                activeIssueNumbers.Add(issue.Number);

                await this.ProcessFilteredIssueAsync(issue: issue, repo: repo, cancellationToken: cancellationToken);
            }

            url = nextUrl;
        }

        return activeIssueNumbers;
    }

    private async Task ProcessScannedPullRequestAsync(
        ApiPullRequest pr,
        string repo,
        List<int> activePrNumbers,
        CancellationToken cancellationToken
    )
    {
        activePrNumbers.Add(pr.Number);

        IReadOnlyList<string> labelNames = [.. pr.Labels.Select(l => l.Name)];

        if (!this.PassesLabelFilter(labelNames))
        {
            return;
        }

        if (pr.HtmlUrl is not { } prHtmlUrl)
        {
            return;
        }

        WorkPriority priority = LabelParser.ParsePriority(labelNames);
        bool isOnHold = LabelParser.IsOnHold(labels: labelNames, noWorkFilter: this._options.Filter.NoWorkFilter);
        string status = pr.Draft ? "Draft" : "Open";

        await this._notificationStateTracker.UpdateStateAsync(
            notification: BuildScanNotification(repo),
            details: BuildScannedPullRequestDetails(
                pr: pr,
                repo: repo,
                labelNames: labelNames,
                status: status,
                htmlUrl: new Uri(prHtmlUrl)
            ),
            priority: priority,
            isOnHold: isOnHold,
            cancellationToken: cancellationToken
        );

        this._logger.LogScannedPullRequest(repo: repo, number: pr.Number, status: status);
    }

    private async Task ProcessFilteredIssueAsync(ApiIssue issue, string repo, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> labelNames = issue.Labels is null ? [] : [.. issue.Labels.Select(l => l.Name)];

        if (!this.PassesLabelFilter(labelNames))
        {
            return;
        }

        if (issue.HtmlUrl is not { } issueHtmlUrl)
        {
            return;
        }

        WorkPriority priority = LabelParser.ParsePriority(labelNames);
        bool isOnHold = LabelParser.IsOnHold(labels: labelNames, noWorkFilter: this._options.Filter.NoWorkFilter);

        await this._notificationStateTracker.UpdateStateAsync(
            notification: BuildScanNotification(repo),
            details: BuildScannedIssueDetails(
                issue: issue,
                repo: repo,
                labelNames: labelNames,
                htmlUrl: new Uri(issueHtmlUrl)
            ),
            priority: priority,
            isOnHold: isOnHold,
            cancellationToken: cancellationToken
        );

        this._logger.LogScannedIssue(repo: repo, number: issue.Number);
    }

    private bool PassesLabelFilter(IReadOnlyList<string> labelNames)
    {
        List<string> filters = [.. this._options.Filter.LabelFilter.Where(static f => !string.IsNullOrWhiteSpace(f))];

        if (filters.Count == 0)
        {
            return true;
        }

        return labelNames.Any(label => filters.Exists(filter => LabelParser.FuzzyEquals(label, filter)));
    }

    private static string GetRepoName(string fullName)
    {
        int slash = fullName.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);

        return slash >= 0 ? fullName[(slash + 1)..] : fullName;
    }

    private static GitHubNotification BuildScanNotification(string repo)
    {
        Uri repoUri = new($"https://github.com/{repo}");

        return new GitHubNotification(
            Id: $"scan:{repo}",
            Reason: "scan",
            Subject: new NotificationSubject(Title: string.Empty, Url: repoUri, Type: string.Empty),
            Repository: new NotificationRepository(FullName: repo, Url: repoUri),
            UpdatedAt: DateTimeOffset.MinValue,
            Unread: false
        );
    }

    private static PullRequestDetails BuildScannedPullRequestDetails(
        ApiPullRequest pr,
        string repo,
        IReadOnlyList<string> labelNames,
        string status,
        Uri htmlUrl
    )
    {
        string owner = GetOwner(repo);
        string name = GetRepoName(repo);
        Uri repoUri = new($"https://github.com/{repo}");

        return new PullRequestDetails(
            Number: pr.Number,
            Title: pr.Title,
            Status: status,
            HtmlUrl: htmlUrl,
            Assignees: [.. pr.Assignees.Select(a => a.Login)],
            Labels: labelNames,
            Body: null,
            Comments: [],
            Reviews: [],
            Runs: [],
            LinkedItems: [],
            Repository: new ItemRepository(Owner: owner, Name: name, Url: repoUri),
            LastNotification: new LastNotification(
                Id: $"scan:{repo}:pr:{pr.Number}",
                Timestamp: DateTimeOffset.MinValue
            ),
            Author: pr.User?.Login
        );
    }

    private static IssueDetails BuildScannedIssueDetails(
        ApiIssue issue,
        string repo,
        IReadOnlyList<string> labelNames,
        Uri htmlUrl
    )
    {
        string owner = GetOwner(repo);
        string name = GetRepoName(repo);
        Uri repoUri = new($"https://github.com/{repo}");

        return new IssueDetails(
            Number: issue.Number,
            Title: issue.Title,
            Status: "Open",
            HtmlUrl: htmlUrl,
            Assignees: [],
            Labels: labelNames,
            LinkedPullRequestUrl: null,
            Repository: new ItemRepository(Owner: owner, Name: name, Url: repoUri),
            LastNotification: new LastNotification(
                Id: $"scan:{repo}:issue:{issue.Number}",
                Timestamp: DateTimeOffset.MinValue
            )
        );
    }
}
