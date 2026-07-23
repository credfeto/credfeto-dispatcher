using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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

public sealed class RepoEventPoller : IRepoEventPoller
{
    private const string PULL_REQUEST_EVENT_TYPE = "PullRequestEvent";
    private const string ISSUES_EVENT_TYPE = "IssuesEvent";

    private readonly IActiveRepoTracker _activeRepoTracker;
    private readonly IETagStore _eTagStore;
    private readonly GitHubRepoHelper _helper;
    private readonly ILogger<RepoEventPoller> _logger;
    private readonly INotificationStateTracker _notificationStateTracker;
    private readonly GitHubOptions _options;

    public RepoEventPoller(
        GitHubRepoHelper helper,
        IActiveRepoTracker activeRepoTracker,
        IETagStore eTagStore,
        INotificationStateTracker notificationStateTracker,
        IOptions<GitHubOptions> options,
        ILogger<RepoEventPoller> logger
    )
    {
        this._helper = helper;
        this._activeRepoTracker = activeRepoTracker;
        this._eTagStore = eTagStore;
        this._notificationStateTracker = notificationStateTracker;
        this._options = options.Value;
        this._logger = logger;
    }

    public async ValueTask PollAsync(CancellationToken cancellationToken)
    {
        if (!this._options.Filter.PollEvents)
        {
            return;
        }

        IReadOnlyList<string> repos = await this._activeRepoTracker.GetActiveReposAsync(cancellationToken);

        if (repos.Count == 0)
        {
            (_, repos, _) = await this._helper.DiscoverReposAsync(
                shouldInclude: this.ShouldIncludeRepo,
                cancellationToken: cancellationToken
            );
        }

        if (repos.Count == 0)
        {
            return;
        }

        IReadOnlyList<string> owners = [.. repos.Select(GetOwner).Distinct(StringComparer.OrdinalIgnoreCase)];

        foreach (string repo in repos)
        {
            await this.PollFeedAsync(
                url: $"repos/{repo}/events?per_page=30",
                lastIdKey: $"events.repo.lastid:{repo}",
                feedDescription: repo,
                cancellationToken: cancellationToken
            );
        }

        foreach (string owner in owners)
        {
            await this.PollFeedAsync(
                url: $"users/{owner}/events?per_page=30",
                lastIdKey: $"events.owner.lastid:{owner}",
                feedDescription: $"@{owner}",
                cancellationToken: cancellationToken
            );
        }

        this._logger.LogEventPollComplete(repoCount: repos.Count, ownerCount: owners.Count);
    }

    private async Task PollFeedAsync(
        string url,
        string lastIdKey,
        string feedDescription,
        CancellationToken cancellationToken
    )
    {
        long lastId = await this.LoadLastIdAsync(key: lastIdKey, cancellationToken: cancellationToken);

        (ApiEvent[]? events, _) = await this._helper.GetPagedAsync(
            url: url,
            jsonTypeInfo: NotificationSerializerContext.Default.ApiEventArray,
            cancellationToken: cancellationToken
        );

        if (events is null || events.Length == 0)
        {
            return;
        }

        (long newestId, int processed) = await this.ProcessNewEventsAsync(
            events: events,
            lastId: lastId,
            cancellationToken: cancellationToken
        );

        if (newestId > lastId)
        {
            await this._eTagStore.SaveETagAsync(
                key: lastIdKey,
                eTag: newestId.ToString(CultureInfo.InvariantCulture),
                cancellationToken: cancellationToken
            );
        }

        if (processed > 0)
        {
            this._logger.LogFeedProcessed(feed: feedDescription, count: processed);
        }
    }

    private async ValueTask<long> LoadLastIdAsync(string key, CancellationToken cancellationToken)
    {
        string? lastIdStr = await this._eTagStore.GetETagAsync(key: key, cancellationToken: cancellationToken);

        return
            lastIdStr is not null
            && long.TryParse(
                s: lastIdStr,
                style: NumberStyles.Integer,
                provider: CultureInfo.InvariantCulture,
                result: out long parsed
            )
            ? parsed
            : 0;
    }

    private async ValueTask<(long newestId, int processed)> ProcessNewEventsAsync(
        ApiEvent[] events,
        long lastId,
        CancellationToken cancellationToken
    )
    {
        long newestId = 0;
        int processed = 0;

        foreach (ApiEvent ev in events)
        {
            if (
                !long.TryParse(
                    s: ev.Id,
                    style: NumberStyles.Integer,
                    provider: CultureInfo.InvariantCulture,
                    result: out long eventId
                )
            )
            {
                continue;
            }

            if (newestId == 0)
            {
                newestId = eventId;
            }

            if (eventId <= lastId)
            {
                break;
            }

            await this.ProcessEventAsync(ev: ev, cancellationToken: cancellationToken);
            processed++;
        }

        return (newestId, processed);
    }

    private async ValueTask ProcessEventAsync(ApiEvent ev, CancellationToken cancellationToken)
    {
        if (string.Equals(a: ev.Type, b: PULL_REQUEST_EVENT_TYPE, comparisonType: StringComparison.Ordinal))
        {
            if (ev.Payload.PullRequest is { HtmlUrl: { } prHtmlUrl } pr)
            {
                await this.ProcessPullRequestEventAsync(
                    ev: ev,
                    pr: pr,
                    htmlUrl: new Uri(prHtmlUrl),
                    cancellationToken: cancellationToken
                );
            }

            return;
        }

        if (string.Equals(a: ev.Type, b: ISSUES_EVENT_TYPE, comparisonType: StringComparison.Ordinal))
        {
            if (ev.Payload.Issue is { HtmlUrl: { } issueHtmlUrl } issue && issue.PullRequest is null)
            {
                await this.ProcessIssueEventAsync(
                    ev: ev,
                    issue: issue,
                    htmlUrl: new Uri(issueHtmlUrl),
                    cancellationToken: cancellationToken
                );
            }
        }
    }

    private async ValueTask ProcessPullRequestEventAsync(
        ApiEvent ev,
        ApiPullRequest pr,
        Uri htmlUrl,
        CancellationToken cancellationToken
    )
    {
        string repo = ev.Repo.Name;
        string owner = GetOwner(repo);
        string name = GetRepoName(repo);
        Uri repoUri = new($"https://github.com/{repo}");

        GitHubNotification notification = BuildEventNotification(
            ev: ev,
            repo: repo,
            itemType: "PullRequest",
            itemUrl: htmlUrl.OriginalString,
            itemTitle: pr.Title
        );

        PullRequestDetails details = new(
            Number: pr.Number,
            Title: pr.Title,
            Status: MapPrStatus(pr: pr),
            HtmlUrl: htmlUrl,
            Assignees: [.. pr.Assignees.Select(a => a.Login)],
            Labels: [.. pr.Labels.Select(l => l.Name)],
            Body: null,
            Comments: [],
            Reviews: [],
            Runs: [],
            LinkedItems: [],
            Repository: new ItemRepository(Owner: owner, Name: name, Url: repoUri),
            LastNotification: new LastNotification(Id: $"event:{repo}:pr:{pr.Number}", Timestamp: ev.CreatedAt),
            Author: pr.User?.Login,
            CommitAuthors: []
        );

        WorkPriority priority = LabelParser.ParsePriority(details.Labels);
        bool isOnHold = LabelParser.IsOnHold(labels: details.Labels, noWorkFilter: this._options.Filter.NoWorkFilter);

        await this._notificationStateTracker.UpdateStateAsync(
            notification: notification,
            details: details,
            priority: priority,
            isOnHold: isOnHold,
            cancellationToken: cancellationToken
        );

        this._logger.LogProcessedPullRequestEvent(
            repo: repo,
            number: pr.Number,
            action: ev.Payload.Action ?? string.Empty
        );
    }

    private async ValueTask ProcessIssueEventAsync(
        ApiEvent ev,
        ApiIssue issue,
        Uri htmlUrl,
        CancellationToken cancellationToken
    )
    {
        string repo = ev.Repo.Name;
        string owner = GetOwner(repo);
        string name = GetRepoName(repo);
        Uri repoUri = new($"https://github.com/{repo}");

        GitHubNotification notification = BuildEventNotification(
            ev: ev,
            repo: repo,
            itemType: "Issue",
            itemUrl: htmlUrl.OriginalString,
            itemTitle: issue.Title
        );

        IssueDetails details = new(
            Number: issue.Number,
            Title: issue.Title,
            Status: MapIssueStatus(issue),
            HtmlUrl: htmlUrl,
            Assignees: issue.Assignees is null ? [] : [.. issue.Assignees.Select(a => a.Login)],
            Labels: issue.Labels is null ? [] : [.. issue.Labels.Select(l => l.Name)],
            LinkedPullRequestUrl: null,
            Repository: new ItemRepository(Owner: owner, Name: name, Url: repoUri),
            LastNotification: new LastNotification(Id: $"event:{repo}:issue:{issue.Number}", Timestamp: ev.CreatedAt)
        );

        WorkPriority priority = LabelParser.ParsePriority(details.Labels);
        bool isOnHold = LabelParser.IsOnHold(labels: details.Labels, noWorkFilter: this._options.Filter.NoWorkFilter);

        await this._notificationStateTracker.UpdateStateAsync(
            notification: notification,
            details: details,
            priority: priority,
            isOnHold: isOnHold,
            cancellationToken: cancellationToken
        );

        this._logger.LogProcessedIssueEvent(
            repo: repo,
            number: issue.Number,
            action: ev.Payload.Action ?? string.Empty
        );
    }

    private static GitHubNotification BuildEventNotification(
        ApiEvent ev,
        string repo,
        string itemType,
        string itemUrl,
        string itemTitle
    )
    {
        Uri repoUri = new($"https://github.com/{repo}");

        return new GitHubNotification(
            Id: $"event:{repo}:{ev.Id}",
            Reason: "event",
            Subject: new NotificationSubject(Title: itemTitle, Url: new Uri(itemUrl), Type: itemType),
            Repository: new NotificationRepository(FullName: repo, Url: repoUri),
            UpdatedAt: ev.CreatedAt,
            Unread: false
        );
    }

    private static string MapPrStatus(ApiPullRequest pr)
    {
        if (string.Equals(a: pr.State, b: "closed", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return "Closed";
        }

        return pr.Draft ? "Draft" : "Open";
    }

    private static string MapIssueStatus(ApiIssue issue)
    {
        return string.Equals(a: issue.State, b: "closed", comparisonType: StringComparison.OrdinalIgnoreCase)
            ? "Closed"
            : "Open";
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical filter pattern shared with WorkItemScanner and ModifiedIssueMentionPoller."
    )]
    private bool ShouldIncludeRepo(ApiUserRepo repo)
    {
        if (repo.Archived || repo.Disabled)
        {
            return false;
        }

        if (repo.Permissions?.Push != true)
        {
            return false;
        }

        return this.PassesRepoFilter(repo.FullName);
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical filter pattern shared with WorkItemScanner and ModifiedIssueMentionPoller."
    )]
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

    private static string GetRepoName(string fullName)
    {
        int slash = fullName.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);

        return slash >= 0 ? fullName[(slash + 1)..] : fullName;
    }
}
