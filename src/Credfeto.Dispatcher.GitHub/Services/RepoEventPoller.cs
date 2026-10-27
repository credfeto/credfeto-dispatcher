using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
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

    // Bounds how far back a single poll follows the feed's next link when every event on a page is newer than lastId.
    private const int MAX_PAGES_PER_FEED = 3;

    // The seen-id list is stored in PollingStates.ETag, which is NVARCHAR(1024).
    private const int MAX_SEEN_IDS_LENGTH = 1024;

    private readonly IReadOnlyList<string> _activeLabelFilter;
    private readonly IActiveRepoTracker _activeRepoTracker;
    private readonly IETagStore _eTagStore;
    private readonly GitHubRepoHelper _helper;
    private readonly ILogger<RepoEventPoller> _logger;
    private readonly INotificationStateTracker _notificationStateTracker;
    private readonly GitHubOptions _options;
    private readonly IPullRequestDetailFetcher _pullRequestDetailFetcher;

    public RepoEventPoller(
        GitHubRepoHelper helper,
        IActiveRepoTracker activeRepoTracker,
        IETagStore eTagStore,
        INotificationStateTracker notificationStateTracker,
        IPullRequestDetailFetcher pullRequestDetailFetcher,
        IOptions<GitHubOptions> options,
        ILogger<RepoEventPoller> logger
    )
    {
        this._helper = helper;
        this._activeRepoTracker = activeRepoTracker;
        this._eTagStore = eTagStore;
        this._notificationStateTracker = notificationStateTracker;
        this._pullRequestDetailFetcher = pullRequestDetailFetcher;
        this._options = options.Value;
        this._activeLabelFilter = LinkedIssueTrust.ActiveLabelFilter(this._options.Filter.LabelFilter);
        this._logger = logger;
    }

    public async ValueTask<int?> PollAsync(CancellationToken cancellationToken)
    {
        if (!this._options.Filter.PollEvents)
        {
            return null;
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
            return null;
        }

        IReadOnlyList<string> owners = [.. repos.Select(GetOwner).Distinct(StringComparer.OrdinalIgnoreCase)];

        int? suggestedPollIntervalSeconds = null;

        foreach (RepoEventFeed feed in BuildFeeds(repos: repos, owners: owners))
        {
            int? feedPollIntervalSeconds = await this.PollFeedAsync(feed: feed, cancellationToken: cancellationToken);
            suggestedPollIntervalSeconds = ETagHeaderUtility.MaxPollIntervalSeconds(
                suggestedPollIntervalSeconds,
                feedPollIntervalSeconds
            );
        }

        this._logger.LogEventPollComplete(repoCount: repos.Count, ownerCount: owners.Count);

        return suggestedPollIntervalSeconds;
    }

    private static IEnumerable<RepoEventFeed> BuildFeeds(IReadOnlyList<string> repos, IReadOnlyList<string> owners)
    {
        return repos
            .Select(repo => new RepoEventFeed(
                Url: $"repos/{repo}/events?per_page=30",
                LastIdKey: $"events.repo.lastid:{repo}",
                SeenIdsKey: $"events.repo.seen:{repo}",
                ETagKey: $"events.repo.etag:{repo}",
                Description: repo
            ))
            .Concat(
                owners.Select(owner => new RepoEventFeed(
                    Url: $"users/{owner}/events?per_page=30",
                    LastIdKey: $"events.owner.lastid:{owner}",
                    SeenIdsKey: $"events.owner.seen:{owner}",
                    ETagKey: $"events.owner.etag:{owner}",
                    Description: $"@{owner}"
                ))
            );
    }

    private async Task<int?> PollFeedAsync(RepoEventFeed feed, CancellationToken cancellationToken)
    {
        string? storedETag = await this._eTagStore.GetETagAsync(
            key: feed.ETagKey,
            cancellationToken: cancellationToken
        );

        PagedETagResult<ApiEvent> result = await this._helper.GetPagedWithETagAsync(
            url: feed.Url,
            jsonTypeInfo: NotificationSerializerContext.Default.ApiEventArray,
            eTag: storedETag,
            cancellationToken: cancellationToken
        );

        if (result.NotModified)
        {
            this._logger.LogFeedNotModified(feed: feed.Description);

            return result.PollIntervalSeconds;
        }

        if (result.Items is not null && result.Items.Length != 0)
        {
            await this.ProcessFeedItemsAsync(
                feed: feed,
                firstPageItems: result.Items,
                nextUrl: result.NextUrl,
                cancellationToken: cancellationToken
            );
        }

        // Saved after processing so a failure mid-processing re-fetches the same events next poll instead of skipping them via 304.
        await this.SaveIfChangedAsync(
            key: feed.ETagKey,
            newValue: result.ETag,
            storedValue: storedETag,
            cancellationToken: cancellationToken
        );

        return result.PollIntervalSeconds;
    }

    private async Task ProcessFeedItemsAsync(
        RepoEventFeed feed,
        ApiEvent[] firstPageItems,
        string? nextUrl,
        CancellationToken cancellationToken
    )
    {
        string? lastIdString = await this._eTagStore.GetETagAsync(
            key: feed.LastIdKey,
            cancellationToken: cancellationToken
        );
        long lastId = ParseLastId(lastIdString);

        string? storedSeenIds = await this._eTagStore.GetETagAsync(
            key: feed.SeenIdsKey,
            cancellationToken: cancellationToken
        );

        (IReadOnlyList<(long Id, ApiEvent Event)> events, int pageCount) = await this.FetchEventsAsync(
            firstPageItems: firstPageItems,
            nextUrl: nextUrl,
            lastId: lastId,
            cancellationToken: cancellationToken
        );

        (int newCount, int upsertedCount, int skippedSeenCount) = await this.ProcessNewEventsAsync(
            events: events,
            seenIds: ParseSeenIds(storedSeenIds),
            cancellationToken: cancellationToken
        );

        long newestId = events.Count == 0 ? 0 : events.Max(e => e.Id);

        if (newestId > lastId)
        {
            await this._eTagStore.SaveETagAsync(
                key: feed.LastIdKey,
                eTag: newestId.ToString(CultureInfo.InvariantCulture),
                cancellationToken: cancellationToken
            );
        }

        await this.SaveIfChangedAsync(
            key: feed.SeenIdsKey,
            newValue: BuildSeenIds(events),
            storedValue: storedSeenIds,
            cancellationToken: cancellationToken
        );

        if (newCount > 0)
        {
            this._logger.LogFeedProcessed(
                feed: feed.Description,
                newCount: newCount,
                upsertedCount: upsertedCount,
                skippedSeenCount: skippedSeenCount,
                pageCount: pageCount,
                lastId: lastId
            );
        }
        else
        {
            this._logger.LogFeedNoNewEvents(feed: feed.Description, skippedSeenCount: skippedSeenCount, lastId: lastId);
        }
    }

    // The feed is newest-first. A burst of more than one page of events between polls would otherwise be truncated,
    // so keep reading while every event on the page is newer than lastId. With no lastId yet there is no gap to fill.
    private async ValueTask<(IReadOnlyList<(long Id, ApiEvent Event)> Events, int PageCount)> FetchEventsAsync(
        ApiEvent[] firstPageItems,
        string? nextUrl,
        long lastId,
        CancellationToken cancellationToken
    )
    {
        List<(long Id, ApiEvent Event)> events = new(firstPageItems.Length);
        bool followNext = AddParsedEvents(page: firstPageItems, lastId: lastId, events: events) && lastId > 0;
        string? url = nextUrl;
        int pageCount = 1;

        while (followNext && url is not null && pageCount < MAX_PAGES_PER_FEED)
        {
            PagedETagResult<ApiEvent> page = await this._helper.GetPagedWithETagAsync(
                url: url,
                jsonTypeInfo: NotificationSerializerContext.Default.ApiEventArray,
                eTag: null,
                cancellationToken: cancellationToken
            );

            if (page.Items is null)
            {
                break;
            }

            ++pageCount;
            followNext = AddParsedEvents(page: page.Items, lastId: lastId, events: events);
            url = page.NextUrl;
        }

        return (events, pageCount);
    }

    private static bool AddParsedEvents(ApiEvent[] page, long lastId, List<(long Id, ApiEvent Event)> events)
    {
        bool allNewerThanLastId = page.Length != 0;

        foreach (ApiEvent ev in page)
        {
            if (!TryParseId(value: ev.Id, id: out long eventId))
            {
                continue;
            }

            if (eventId <= lastId)
            {
                allNewerThanLastId = false;
            }

            events.Add((eventId, ev));
        }

        return allNewerThanLastId;
    }

    private async Task SaveIfChangedAsync(
        string key,
        string? newValue,
        string? storedValue,
        CancellationToken cancellationToken
    )
    {
        if (newValue is not null && !StringComparer.Ordinal.Equals(newValue, storedValue))
        {
            await this._eTagStore.SaveETagAsync(key: key, eTag: newValue, cancellationToken: cancellationToken);
        }
    }

    private static long ParseLastId(string? lastIdStr)
    {
        return TryParseId(value: lastIdStr, id: out long parsed) ? parsed : 0;
    }

    private static bool TryParseId(string? value, out long id)
    {
        return long.TryParse(
            s: value,
            style: NumberStyles.Integer,
            provider: CultureInfo.InvariantCulture,
            result: out id
        );
    }

    private static IReadOnlySet<long> ParseSeenIds(string? storedSeenIds)
    {
        HashSet<long> ids = [];

        if (string.IsNullOrEmpty(storedSeenIds))
        {
            return ids;
        }

        foreach (string part in storedSeenIds.Split(separator: ',', options: StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseId(value: part, id: out long id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    // Everything read this poll is now either processed or already seen. Events that have dropped off the pages
    // read cannot reappear above them, so only the ids read this poll are kept, newest-first, within the column limit.
    private static string BuildSeenIds(IReadOnlyList<(long Id, ApiEvent Event)> events)
    {
        StringBuilder builder = new();
        HashSet<long> added = [];

        foreach ((long id, _) in events)
        {
            if (!added.Add(id))
            {
                continue;
            }

            string text = id.ToString(CultureInfo.InvariantCulture);
            int separatorLength = builder.Length == 0 ? 0 : 1;

            if (builder.Length + separatorLength + text.Length > MAX_SEEN_IDS_LENGTH)
            {
                break;
            }

            if (separatorLength != 0)
            {
                builder.Append(',');
            }

            builder.Append(text);
        }

        return builder.ToString();
    }

    // Processes every event read this poll that has not been seen before, rather than stopping at lastId: the
    // Events API can deliver an event after a newer one has already been read, and upserts are idempotent.
    private async ValueTask<(int NewCount, int UpsertedCount, int SkippedSeenCount)> ProcessNewEventsAsync(
        IReadOnlyList<(long Id, ApiEvent Event)> events,
        IReadOnlySet<long> seenIds,
        CancellationToken cancellationToken
    )
    {
        HashSet<long> considered = [];
        List<ApiEvent> newEvents = new(events.Count);
        int skippedSeenCount = 0;

        foreach ((long id, ApiEvent ev) in events)
        {
            // Pages can overlap when new events arrive between page requests.
            if (!considered.Add(id))
            {
                continue;
            }

            if (seenIds.Contains(id))
            {
                ++skippedSeenCount;

                continue;
            }

            newEvents.Add(ev);
        }

        int upsertedCount = 0;

        // Feed is newest-first; apply oldest-first so the newest event's state wins the upsert.
        for (int i = newEvents.Count - 1; i >= 0; --i)
        {
            if (await this.ProcessEventAsync(ev: newEvents[i], cancellationToken: cancellationToken))
            {
                ++upsertedCount;
            }
        }

        return (newEvents.Count, upsertedCount, skippedSeenCount);
    }

    private async ValueTask<bool> ProcessEventAsync(ApiEvent ev, CancellationToken cancellationToken)
    {
        if (string.Equals(a: ev.Type, b: PULL_REQUEST_EVENT_TYPE, comparisonType: StringComparison.Ordinal))
        {
            return ev.Payload.PullRequest is { HtmlUrl: { } prHtmlUrl } pr
                && await this.ProcessPullRequestEventAsync(
                    ev: ev,
                    pr: pr,
                    htmlUrl: new Uri(prHtmlUrl),
                    cancellationToken: cancellationToken
                );
        }

        if (string.Equals(a: ev.Type, b: ISSUES_EVENT_TYPE, comparisonType: StringComparison.Ordinal))
        {
            return ev.Payload.Issue is { HtmlUrl: { } issueHtmlUrl } issue
                && issue.PullRequest is null
                && await this.ProcessIssueEventAsync(
                    ev: ev,
                    issue: issue,
                    htmlUrl: new Uri(issueHtmlUrl),
                    cancellationToken: cancellationToken
                );
        }

        return false;
    }

    private async ValueTask<bool> ProcessPullRequestEventAsync(
        ApiEvent ev,
        ApiPullRequest pr,
        Uri htmlUrl,
        CancellationToken cancellationToken
    )
    {
        string repo = ev.Repo.Name;
        IReadOnlyList<string> labelNames = [.. pr.Labels.Select(l => l.Name)];

        PullRequestEligibility eligibility = await this.ResolvePullRequestEligibilityAsync(
            pr: pr,
            repo: repo,
            labelNames: labelNames,
            cancellationToken: cancellationToken
        );

        if (!eligibility.Allowed)
        {
            return false;
        }

        GitHubNotification notification = BuildEventNotification(
            ev: ev,
            repo: repo,
            itemType: "PullRequest",
            itemUrl: htmlUrl.OriginalString,
            itemTitle: pr.Title
        );

        PullRequestDetails details = BuildEventPullRequestDetails(
            ev: ev,
            pr: pr,
            repo: repo,
            labelNames: labelNames,
            htmlUrl: htmlUrl,
            linkedItems: eligibility.LinkedItems,
            commitAuthors: eligibility.CommitAuthors
        );

        WorkPriority priority = LinkedIssueTrust.ElevatePriority(
            LabelParser.ParsePriority(details.Labels),
            eligibility.TrustedLinkedIssue
        );

        bool isOnHold = LabelParser.IsOnHold(labels: details.Labels, noWorkFilter: this._options.Filter.NoWorkFilter);

        await this._notificationStateTracker.UpdateStateAsync(
            notification: notification,
            details: details,
            priority: priority,
            isOnHold: isOnHold,
            isAdopted: null,
            cancellationToken: cancellationToken
        );

        this._logger.LogProcessedPullRequestEvent(
            repo: repo,
            number: pr.Number,
            action: ev.Payload.Action ?? string.Empty,
            isOnHold: isOnHold
        );

        return true;
    }

    private static PullRequestDetails BuildEventPullRequestDetails(
        ApiEvent ev,
        ApiPullRequest pr,
        string repo,
        IReadOnlyList<string> labelNames,
        Uri htmlUrl,
        IReadOnlyList<LinkedItem> linkedItems,
        IReadOnlyList<string> commitAuthors
    )
    {
        return PullRequestDetailsFactory.Build(
            pr: pr,
            repo: repo,
            labelNames: labelNames,
            status: MapPrStatus(pr),
            htmlUrl: htmlUrl,
            linkedItems: linkedItems,
            commitAuthors: commitAuthors,
            lastNotificationId: $"event:{repo}:pr:{pr.Number}",
            lastNotificationTimestamp: ev.CreatedAt
        );
    }

    private bool PassesLabelFilter(IReadOnlyList<string> labelNames)
    {
        return LinkedIssueTrust.PassesLabelFilter(labelNames, this._activeLabelFilter);
    }

    // A PR whose own labels fail the filter is still trusted when it unambiguously resolves the
    // author's own assigned, label-passing issue - matching the same rule applied during full scans
    // and notification polling.
    private async Task<PullRequestEligibility> ResolvePullRequestEligibilityAsync(
        ApiPullRequest pr,
        string repo,
        IReadOnlyList<string> labelNames,
        CancellationToken cancellationToken
    )
    {
        if (this.PassesLabelFilter(labelNames))
        {
            return PullRequestEligibility.AllowedWithoutEnrichment;
        }

        GitHubNotification apiNotification = SyntheticNotifications.BuildPullRequestApiNotification(
            idPrefix: "event-enrich",
            repo: repo,
            number: pr.Number
        );

        PullRequestDetails? enriched = await this._pullRequestDetailFetcher.FetchAsync(
            notification: apiNotification,
            cancellationToken: cancellationToken
        );

        if (enriched is null)
        {
            return PullRequestEligibility.Rejected;
        }

        LinkedItem? trustedLinkedIssue = LinkedIssueTrust.FindTrustedLinkedIssue(
            author: enriched.Author,
            commitAuthors: enriched.CommitAuthors,
            linkedItems: enriched.LinkedItems,
            activeLabelFilter: this._activeLabelFilter
        );

        return trustedLinkedIssue is null
            ? PullRequestEligibility.Rejected
            : PullRequestEligibility.Trusted(enriched: enriched, trustedLinkedIssue: trustedLinkedIssue);
    }

    private async ValueTask<bool> ProcessIssueEventAsync(
        ApiEvent ev,
        ApiIssue issue,
        Uri htmlUrl,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> labelNames = issue.Labels is null ? [] : [.. issue.Labels.Select(l => l.Name)];

        if (!this.PassesLabelFilter(labelNames))
        {
            return false;
        }

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
            Assignees: [.. issue.Assignees?.Select(a => a.Login) ?? []],
            Labels: labelNames,
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
            action: ev.Payload.Action ?? string.Empty,
            isOnHold: isOnHold
        );

        return true;
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
