using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Models;
using Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.Services;

// CI never appears in the repository events API, so each poll cycle re-reads the workflow runs of a few open,
// non-on-hold PRs whose checks are unknown, pending or failed, least recently checked first. Each costs one
// conditional request (a 304 when nothing changed); only a PR whose runs changed is re-fetched in full.
public sealed class PullRequestCheckRefresher : IPullRequestCheckRefresher
{
    private readonly PullRequestCheckTracker _checkTracker;
    private readonly GitHubRepoHelper _helper;
    private readonly ILogger<PullRequestCheckRefresher> _logger;
    private readonly GitHubOptions _options;
    private readonly IWorkItemRefresher _workItemRefresher;

    public PullRequestCheckRefresher(
        GitHubRepoHelper helper,
        PullRequestCheckTracker checkTracker,
        IWorkItemRefresher workItemRefresher,
        IOptions<GitHubOptions> options,
        ILogger<PullRequestCheckRefresher> logger
    )
    {
        this._helper = helper;
        this._checkTracker = checkTracker;
        this._workItemRefresher = workItemRefresher;
        this._options = options.Value;
        this._logger = logger;
    }

    public async ValueTask<int?> RefreshAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PullRequestCheckState> candidates = this._checkTracker.SelectCandidates(
            this._options.Filter.MaxCheckRefreshesPerPoll
        );

        if (candidates.Count == 0)
        {
            return null;
        }

        int? suggestedPollIntervalSeconds = null;
        int notModifiedCount = 0;
        int refreshedCount = 0;

        foreach (PullRequestCheckState candidate in candidates)
        {
            CheckOutcome outcome = await this.CheckAsync(candidate: candidate, cancellationToken: cancellationToken);
            suggestedPollIntervalSeconds = ETagHeaderUtility.MaxPollIntervalSeconds(
                suggestedPollIntervalSeconds,
                outcome.PollIntervalSeconds
            );

            if (outcome.Kind == CheckOutcomeKind.RATE_LIMITED)
            {
                this._logger.LogCheckRefreshRateLimited(repo: candidate.Repo, number: candidate.Number);

                break;
            }

            notModifiedCount += outcome.Kind == CheckOutcomeKind.UNCHANGED ? 1 : 0;
            refreshedCount += outcome.Kind == CheckOutcomeKind.REFRESHED ? 1 : 0;
        }

        this._logger.LogCheckRefreshComplete(
            candidateCount: candidates.Count,
            unchangedCount: notModifiedCount,
            refreshedCount: refreshedCount,
            trackedCount: this._checkTracker.Count
        );

        return suggestedPollIntervalSeconds;
    }

    private async ValueTask<CheckOutcome> CheckAsync(
        PullRequestCheckState candidate,
        CancellationToken cancellationToken
    )
    {
        if (candidate.HeadSha is null)
        {
            // Nothing to ask the runs API about yet: the full fetch learns the head commit and its runs.
            return await this.RefreshPullRequestAsync(
                candidate: candidate,
                eTag: null,
                pollIntervalSeconds: null,
                cancellationToken: cancellationToken
            );
        }

        ETagResult<ApiWorkflowRunsResponse> result = await this._helper.GetWithETagAsync(
            url: BuildRunsUrl(candidate.Repo, candidate.HeadSha),
            jsonTypeInfo: NotificationSerializerContext.Default.ApiWorkflowRunsResponse,
            eTag: candidate.ETag,
            cancellationToken: cancellationToken
        );

        if (result.NotModified)
        {
            this._checkTracker.RecordChecked(checkedState: candidate, eTag: result.ETag);

            return new(CheckOutcomeKind.UNCHANGED, result.PollIntervalSeconds);
        }

        if (result.FailureStatus is { } failureStatus)
        {
            if (IsRateLimited(failureStatus))
            {
                return new(CheckOutcomeKind.RATE_LIMITED, result.PollIntervalSeconds);
            }

            this._checkTracker.RecordChecked(checkedState: candidate, eTag: null);

            return new(CheckOutcomeKind.FAILED, result.PollIntervalSeconds);
        }

        (string signature, _) = CheckRunSignature.Compute([
            .. (result.Item?.WorkflowRuns ?? []).Select(static r => (r.Name, r.Status, r.Conclusion)),
        ]);

        if (StringComparer.Ordinal.Equals(signature, candidate.RunSignature))
        {
            this._checkTracker.RecordChecked(checkedState: candidate, eTag: result.ETag);

            return new(CheckOutcomeKind.UNCHANGED, result.PollIntervalSeconds);
        }

        return await this.RefreshPullRequestAsync(
            candidate: candidate,
            eTag: result.ETag,
            pollIntervalSeconds: result.PollIntervalSeconds,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask<CheckOutcome> RefreshPullRequestAsync(
        PullRequestCheckState candidate,
        string? eTag,
        int? pollIntervalSeconds,
        CancellationToken cancellationToken
    )
    {
        bool refreshed = await this._workItemRefresher.RefreshPullRequestAsync(
            repo: candidate.Repo,
            number: candidate.Number,
            cancellationToken: cancellationToken
        );

        if (!refreshed)
        {
            // Not eligible any more (the refresher forgets it) or the fetch failed: don't retry it every cycle.
            if (this._checkTracker.Get(candidate.Repo, candidate.Number) is not null)
            {
                this._checkTracker.RecordChecked(checkedState: candidate, eTag: null);
            }

            return new(CheckOutcomeKind.FAILED, pollIntervalSeconds);
        }

        // The ETag belongs to the runs of the head commit the request was made for; RecordChecked ignores it when
        // the refresh found a different head.
        if (eTag is not null)
        {
            this._checkTracker.RecordChecked(checkedState: candidate, eTag: eTag);
        }

        return new(CheckOutcomeKind.REFRESHED, pollIntervalSeconds);
    }

    private static bool IsRateLimited(HttpStatusCode status)
    {
        return status is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests;
    }

    internal static string BuildRunsUrl(string repo, string headSha)
    {
        return $"repos/{repo}/actions/runs?head_sha={Uri.EscapeDataString(headSha)}&per_page=100";
    }
}
