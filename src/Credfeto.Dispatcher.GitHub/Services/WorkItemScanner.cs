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

    public WorkItemScanner(
        GitHubRepoHelper helper,
        IActiveRepoTracker activeRepoTracker,
        INotificationStateTracker notificationStateTracker,
        IOptions<GitHubOptions> options,
        ILogger<WorkItemScanner> logger
    )
    {
        this._helper = helper;
        this._activeRepoTracker = activeRepoTracker;
        this._notificationStateTracker = notificationStateTracker;
        this._options = options.Value;
        this._logger = logger;
    }

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> repos = await this._helper.DiscoverReposAsync(
            shouldInclude: this.ShouldIncludeRepo,
            cancellationToken: cancellationToken
        );

        await this._activeRepoTracker.UpdateActiveReposAsync(activeRepos: repos, cancellationToken: cancellationToken);

        if (repos.Count == 0)
        {
            this._logger.LogNoReposDiscovered();

            return;
        }

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

        await this.ScanPullRequestsAsync(repo: repo, cancellationToken: cancellationToken);
        await this.ScanIssuesAsync(repo: repo, cancellationToken: cancellationToken);
    }

    private async Task ScanPullRequestsAsync(string repo, CancellationToken cancellationToken)
    {
        string? url = $"repos/{repo}/pulls?state=open&per_page=100";

        while (url is not null)
        {
            (ApiPullRequest[]? items, string? nextUrl) = await this._helper.GetPagedAsync(
                url: url,
                jsonTypeInfo: NotificationSerializerContext.Default.ApiPullRequestArray,
                cancellationToken: cancellationToken
            );

            if (items is null)
            {
                break;
            }

            foreach (ApiPullRequest pr in items)
            {
                IReadOnlyList<string> labelNames = [.. pr.Labels.Select(l => l.Name)];

                if (!this.PassesLabelFilter(labelNames))
                {
                    continue;
                }

                WorkPriority priority = LabelParser.ParsePriority(labelNames);
                bool isOnHold = LabelParser.IsOnHold(
                    labels: labelNames,
                    noWorkFilter: this._options.Filter.NoWorkFilter
                );
                string status = pr.Draft ? "Draft" : "Open";

                await this._notificationStateTracker.UpdateStateAsync(
                    notification: BuildScanNotification(repo),
                    details: BuildScannedPullRequestDetails(pr: pr, repo: repo, labelNames: labelNames, status: status),
                    priority: priority,
                    isOnHold: isOnHold,
                    cancellationToken: cancellationToken
                );

                this._logger.LogScannedPullRequest(repo: repo, number: pr.Number, status: status);
            }

            url = nextUrl;
        }
    }

    private async Task ScanIssuesAsync(string repo, CancellationToken cancellationToken)
    {
        string? url = $"repos/{repo}/issues?state=open&per_page=100";

        while (url is not null)
        {
            (ApiIssue[]? items, string? nextUrl) = await this._helper.GetPagedAsync(
                url: url,
                jsonTypeInfo: NotificationSerializerContext.Default.ApiIssueArray,
                cancellationToken: cancellationToken
            );

            if (items is null)
            {
                break;
            }

            foreach (ApiIssue issue in items)
            {
                if (issue.PullRequest is not null)
                {
                    continue;
                }

                IReadOnlyList<string> labelNames = issue.Labels is null ? [] : [.. issue.Labels.Select(l => l.Name)];

                if (!this.PassesLabelFilter(labelNames))
                {
                    continue;
                }

                WorkPriority priority = LabelParser.ParsePriority(labelNames);
                bool isOnHold = LabelParser.IsOnHold(
                    labels: labelNames,
                    noWorkFilter: this._options.Filter.NoWorkFilter
                );

                await this._notificationStateTracker.UpdateStateAsync(
                    notification: BuildScanNotification(repo),
                    details: BuildScannedIssueDetails(issue: issue, repo: repo, labelNames: labelNames),
                    priority: priority,
                    isOnHold: isOnHold,
                    cancellationToken: cancellationToken
                );

                this._logger.LogScannedIssue(repo: repo, number: issue.Number);
            }

            url = nextUrl;
        }
    }

    private bool PassesLabelFilter(IReadOnlyList<string> labelNames)
    {
        if (this._options.Filter.LabelFilter.Count == 0)
        {
            return true;
        }

        return labelNames.Any(label =>
            this._options.Filter.LabelFilter.Any(filter => LabelParser.FuzzyEquals(label, filter))
        );
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
        string status
    )
    {
        string owner = GetOwner(repo);
        string name = GetRepoName(repo);
        Uri repoUri = new($"https://github.com/{repo}");

        return new PullRequestDetails(
            Number: pr.Number,
            Title: pr.Title,
            Status: status,
            HtmlUrl: new Uri(pr.HtmlUrl),
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

    private static IssueDetails BuildScannedIssueDetails(ApiIssue issue, string repo, IReadOnlyList<string> labelNames)
    {
        string owner = GetOwner(repo);
        string name = GetRepoName(repo);
        Uri repoUri = new($"https://github.com/{repo}");

        return new IssueDetails(
            Number: issue.Number,
            Title: issue.Title,
            Status: "Open",
            HtmlUrl: new Uri(issue.HtmlUrl),
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
