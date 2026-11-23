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
    private const string PULL_REQUEST_REVIEW_EVENT_TYPE = "PullRequestReviewEvent";
    private const string PULL_REQUEST_REVIEW_COMMENT_EVENT_TYPE = "PullRequestReviewCommentEvent";
    private const string ISSUE_COMMENT_EVENT_TYPE = "IssueCommentEvent";
    private const string PUSH_EVENT_TYPE = "PushEvent";
    private const string BRANCH_REF_PREFIX = "refs/heads/";
    private const string BOT_USER_TYPE = "Bot";
    private const string BOT_LOGIN_SUFFIX = "[bot]";

    // Bounds how far back a single poll follows the feed's next link when every event on a page is newer than lastId.
    private const int MAX_PAGES_PER_FEED = 3;

    // The seen-id list is stored in PollingStates.ETag, which is NVARCHAR(1024).
    private const int MAX_SEEN_IDS_LENGTH = 1024;

    private readonly IReadOnlyList<string> _activeLabelFilter;
    private readonly IActiveRepoTracker _activeRepoTracker;
    private readonly IPullRequestCheckRefresher _checkRefresher;
    private readonly PullRequestCheckTracker _checkTracker;
    private readonly IETagStore _eTagStore;
    private readonly IFeedFreshnessStore _feedFreshnessStore;
    private readonly GitHubRepoHelper _helper;
    private readonly ILogger<RepoEventPoller> _logger;
    private readonly INotificationStateTracker _notificationStateTracker;
    private readonly GitHubOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IWorkItemRefresher _workItemRefresher;

    public RepoEventPoller(
        GitHubRepoHelper helper,
        IActiveRepoTracker activeRepoTracker,
        IETagStore eTagStore,
        INotificationStateTracker notificationStateTracker,
        IWorkItemRefresher workItemRefresher,
        IPullRequestCheckRefresher checkRefresher,
        PullRequestCheckTracker checkTracker,
        IFeedFreshnessStore feedFreshnessStore,
        IOptions<GitHubOptions> options,
        TimeProvider timeProvider,
        ILogger<RepoEventPoller> logger
    )
    {
        this._helper = helper;
        this._activeRepoTracker = activeRepoTracker;
        this._eTagStore = eTagStore;
        this._notificationStateTracker = notificationStateTracker;
        this._workItemRefresher = workItemRefresher;
        this._checkRefresher = checkRefresher;
        this._checkTracker = checkTracker;
        this._feedFreshnessStore = feedFreshnessStore;
        this._options = options.Value;
        this._activeLabelFilter = LinkedIssueTrust.ActiveLabelFilter(this._options.Filter.LabelFilter);
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    public async ValueTask<int?> PollAsync(CancellationToken cancellationToken)
    {
        if (!this._options.Filter.PollEvents)
        {
            return null;
        }

        DateTimeOffset cycleStart = this._timeProvider.GetUtcNow();

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
        EventRefreshQueue refreshQueue = new(repos);

        (int? suggestedPollIntervalSeconds, bool allFeedsRead) = await this.PollFeedsAsync(
            repos: repos,
            owners: owners,
            refreshQueue: refreshQueue,
            cancellationToken: cancellationToken
        );

        bool allRefreshed = await this.RefreshQueuedItemsAsync(
            refreshQueue: refreshQueue,
            cancellationToken: cancellationToken
        );

        // Every feed answered and every item its events touched was re-read: the store is in sync as of the start
        // of this cycle, which is what /priorities reports as asOf.
        if (allFeedsRead && allRefreshed)
        {
            await this._feedFreshnessStore.RecordSyncAsync(syncedAt: cycleStart, cancellationToken: cancellationToken);
        }

        int? checkPollIntervalSeconds = await this._checkRefresher.RefreshAsync(cancellationToken);

        this._logger.LogEventPollComplete(repoCount: repos.Count, ownerCount: owners.Count);

        return ETagHeaderUtility.MaxPollIntervalSeconds(suggestedPollIntervalSeconds, checkPollIntervalSeconds);
    }

    private async ValueTask<(int? PollIntervalSeconds, bool AllFeedsRead)> PollFeedsAsync(
        IReadOnlyList<string> repos,
        IReadOnlyList<string> owners,
        EventRefreshQueue refreshQueue,
        CancellationToken cancellationToken
    )
    {
        int? suggestedPollIntervalSeconds = null;
        bool allFeedsRead = true;

        foreach (RepoEventFeed feed in BuildFeeds(repos: repos, owners: owners))
        {
            (int? feedPollIntervalSeconds, bool feedRead) = await this.PollFeedAsync(
                feed: feed,
                refreshQueue: refreshQueue,
                cancellationToken: cancellationToken
            );
            suggestedPollIntervalSeconds = ETagHeaderUtility.MaxPollIntervalSeconds(
                suggestedPollIntervalSeconds,
                feedPollIntervalSeconds
            );
            allFeedsRead &= feedRead;
        }

        return (suggestedPollIntervalSeconds, allFeedsRead);
    }

    // Branch lookups first (they add PRs to the queue), then the PR re-reads, then the issue re-reads, all within one
    // call budget. PRs past the budget are marked stale for the CI refresh, which works through them on later cycles;
    // issues past it wait for the next scan. Either way the cycle does not count as a complete sync.
    private async ValueTask<bool> RefreshQueuedItemsAsync(
        EventRefreshQueue refreshQueue,
        CancellationToken cancellationToken
    )
    {
        EventRefreshBudget budget = new(this._options.Filter.MaxEventRefreshesPerPoll);

        foreach ((string repo, string branch) in refreshQueue.Branches)
        {
            if (budget.TrySpendLookup())
            {
                await this.QueueOpenPullRequestsForBranchAsync(
                    repo: repo,
                    branch: branch,
                    refreshQueue: refreshQueue,
                    cancellationToken: cancellationToken
                );
            }
        }

        foreach ((string repo, int number) in refreshQueue.PullRequests)
        {
            if (budget.TrySpendPullRequest())
            {
                await this._workItemRefresher.RefreshPullRequestAsync(
                    repo: repo,
                    number: number,
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                this._checkTracker.MarkStale(repo: repo, number: number);
            }
        }

        await this.RefreshQueuedIssuesAsync(
            refreshQueue: refreshQueue,
            budget: budget,
            cancellationToken: cancellationToken
        );

        if (!budget.IsUnused)
        {
            this._logger.LogEventRefreshesComplete(
                lookupCount: budget.LookupCount,
                refreshedCount: budget.PullRequestCount,
                issueCount: budget.IssueCount,
                deferredCount: budget.DeferredCount
            );
        }

        return budget.DeferredCount == 0;
    }

    private async ValueTask RefreshQueuedIssuesAsync(
        EventRefreshQueue refreshQueue,
        EventRefreshBudget budget,
        CancellationToken cancellationToken
    )
    {
        foreach ((string repo, int number, DateTimeOffset commentedAt) in refreshQueue.Issues)
        {
            if (!budget.TrySpendIssue())
            {
                continue;
            }

            GitHubNotification notification = SyntheticNotifications.BuildIssueApiNotification(
                idPrefix: "event-refresh",
                repo: repo,
                number: number,
                updatedAt: commentedAt
            );

            await this._workItemRefresher.RefreshIssueAsync(
                notification: notification,
                cancellationToken: cancellationToken
            );
        }
    }

    // A push to a branch changes the head commit (and so the CI) of any open PR from that branch.
    private async ValueTask QueueOpenPullRequestsForBranchAsync(
        string repo,
        string branch,
        EventRefreshQueue refreshQueue,
        CancellationToken cancellationToken
    )
    {
        (ApiPullRequest[]? items, _, _) = await this._helper.GetPagedAsync(
            url: BuildOpenPullRequestsForBranchUrl(repo: repo, branch: branch),
            jsonTypeInfo: NotificationSerializerContext.Default.ApiPullRequestArray,
            cancellationToken: cancellationToken
        );

        foreach (ApiPullRequest pr in items ?? [])
        {
            refreshQueue.AddPullRequest(repo: repo, number: pr.Number);
        }
    }

    internal static string BuildOpenPullRequestsForBranchUrl(string repo, string branch)
    {
        string head = string.Concat(Uri.EscapeDataString(GetOwner(repo)), ":", Uri.EscapeDataString(branch));

        return $"repos/{repo}/pulls?state=open&head={head}&per_page=10";
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

    private async Task<(int? PollIntervalSeconds, bool Read)> PollFeedAsync(
        RepoEventFeed feed,
        EventRefreshQueue refreshQueue,
        CancellationToken cancellationToken
    )
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

            return (result.PollIntervalSeconds, true);
        }

        if (result.Items is null)
        {
            return (result.PollIntervalSeconds, false);
        }

        if (result.Items.Length != 0)
        {
            await this.ProcessFeedItemsAsync(
                feed: feed,
                firstPageItems: result.Items,
                nextUrl: result.NextUrl,
                refreshQueue: refreshQueue,
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

        return (result.PollIntervalSeconds, true);
    }

    private async Task ProcessFeedItemsAsync(
        RepoEventFeed feed,
        ApiEvent[] firstPageItems,
        string? nextUrl,
        EventRefreshQueue refreshQueue,
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
            refreshQueue: refreshQueue,
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
        List<(long Id, ApiEvent Event)> events = [with(firstPageItems.Length)];
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
        EventRefreshQueue refreshQueue,
        CancellationToken cancellationToken
    )
    {
        HashSet<long> considered = [];
        List<ApiEvent> newEvents = [with(events.Count)];
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
            if (
                await this.ProcessEventAsync(
                    ev: newEvents[i],
                    refreshQueue: refreshQueue,
                    cancellationToken: cancellationToken
                )
            )
            {
                ++upsertedCount;
            }
        }

        return (newEvents.Count, upsertedCount, skippedSeenCount);
    }

    // Issue events carry the issue's labels and state, so a complete one is upserted straight from the payload. Every
    // other event that changes a served item is queued for a full re-read by number: PR events (whose pull_request
    // payload the Events API now trims to base, head, id, number and url), reviews, comments, pushes to a PR's branch,
    // and issue events whose payload is missing a field the upsert needs.
    private async ValueTask<bool> ProcessEventAsync(
        ApiEvent ev,
        EventRefreshQueue refreshQueue,
        CancellationToken cancellationToken
    )
    {
        if (
            StringComparer.Ordinal.Equals(ev.Type, ISSUES_EVENT_TYPE)
            && ev.Payload.Issue
                is {
                    Number: > 0,
                    HtmlUrl: { } issueHtmlUrl,
                    Title: { },
                    State: { },
                    Labels: { },
                    PullRequest: null,
                } issue
        )
        {
            return await this.ProcessIssueEventAsync(
                ev: ev,
                issue: issue,
                htmlUrl: new Uri(issueHtmlUrl),
                cancellationToken: cancellationToken
            );
        }

        this.QueueRefresh(ev: ev, refreshQueue: refreshQueue);

        return false;
    }

    internal bool QueueRefresh(ApiEvent ev, EventRefreshQueue refreshQueue)
    {
        string repo = ev.Repo.Name;

        return ev.Type switch
        {
            PULL_REQUEST_EVENT_TYPE or PULL_REQUEST_REVIEW_EVENT_TYPE or PULL_REQUEST_REVIEW_COMMENT_EVENT_TYPE =>
                refreshQueue.AddPullRequest(repo: repo, number: GetPullRequestNumber(ev.Payload)),
            ISSUES_EVENT_TYPE => QueueIssueEventRefresh(ev: ev, repo: repo, refreshQueue: refreshQueue),
            ISSUE_COMMENT_EVENT_TYPE => this.QueueCommentRefresh(ev: ev, repo: repo, refreshQueue: refreshQueue),
            PUSH_EVENT_TYPE => ev.Payload.Ref is { } pushedRef
                && pushedRef.StartsWith(value: BRANCH_REF_PREFIX, comparisonType: StringComparison.Ordinal)
                && refreshQueue.AddBranch(repo: repo, branch: pushedRef[BRANCH_REF_PREFIX.Length..]),
            _ => false,
        };
    }

    // An issue event whose payload is incomplete re-reads the issue by number rather than being dropped.
    private static bool QueueIssueEventRefresh(ApiEvent ev, string repo, EventRefreshQueue refreshQueue)
    {
        if (ev.Payload.Issue is not { } issue)
        {
            return false;
        }

        int number = issue.Number > 0 ? issue.Number : ParseTrailingNumber(issue.Url);

        return issue.PullRequest is null
            ? refreshQueue.AddIssue(repo: repo, number: number, commentedAt: ev.CreatedAt)
            : refreshQueue.AddPullRequest(repo: repo, number: number);
    }

    // The PR's number from the payload: pull_request.number, else the top-level number, else the tail of
    // pull_request.url (an api.github.com/repos/{owner}/{repo}/pulls/{number} URL).
    internal static int GetPullRequestNumber(ApiEventPayload payload)
    {
        if (payload.PullRequest is { Number: > 0 } pr)
        {
            return pr.Number;
        }

        if (payload.Number is int number && number > 0)
        {
            return number;
        }

        return ParseTrailingNumber(payload.PullRequest?.Url);
    }

    internal static int ParseTrailingNumber(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return 0;
        }

        string trimmed = url.TrimEnd('/');
        int slash = trimmed.AsSpan().LastIndexOf('/');

        return int.TryParse(
            s: trimmed[(slash + 1)..],
            style: NumberStyles.None,
            provider: CultureInfo.InvariantCulture,
            result: out int parsed
        )
            ? parsed
            : 0;
    }

    // A comment on a PR re-reads the PR. A comment on a plain issue re-reads the issue so its stored state is current,
    // unless a bot or a login in Filter:IgnoredCommenters wrote it: re-reading for those would spend the shared API
    // budget on automated traffic.
    private bool QueueCommentRefresh(ApiEvent ev, string repo, EventRefreshQueue refreshQueue)
    {
        if (ev.Payload.Issue is not { } issue)
        {
            return false;
        }

        if (issue.PullRequest is not null)
        {
            return refreshQueue.AddPullRequest(repo: repo, number: issue.Number);
        }

        return !this.IsIgnoredCommentAuthor(ev)
            && refreshQueue.AddIssue(repo: repo, number: issue.Number, commentedAt: ev.CreatedAt);
    }

    private bool IsIgnoredCommentAuthor(ApiEvent ev)
    {
        ApiUser? commenter = ev.Payload.Comment?.User;

        return IsBot(ev.Actor)
            || IsBot(commenter)
            || this.IsIgnoredCommenter(ev.Actor)
            || this.IsIgnoredCommenter(commenter);
    }

    private static bool IsBot(ApiUser? user)
    {
        if (user is null)
        {
            return false;
        }

        if (string.Equals(a: user.Type, b: BOT_USER_TYPE, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrEmpty(user.Login)
            && user.Login.EndsWith(value: BOT_LOGIN_SUFFIX, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private bool IsIgnoredCommenter(ApiUser? user)
    {
        return user is { Login: { } login }
            && this._options.Filter.IgnoredCommenters.Any(ignored =>
                string.Equals(a: ignored, b: login, comparisonType: StringComparison.OrdinalIgnoreCase)
            );
    }

    private bool PassesLabelFilter(IReadOnlyList<string> labelNames)
    {
        return LinkedIssueTrust.PassesLabelFilter(labelNames, this._activeLabelFilter);
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
