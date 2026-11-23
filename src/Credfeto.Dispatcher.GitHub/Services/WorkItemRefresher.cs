using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.Services;

// Re-reads one PR or issue in full and upserts it: the single-item path shared by the notification worker, the
// event poller and the CI refresh. A PR is stored when its own labels pass the filter, an adoption rule matches it
// (with the same priority the full scan gives it), or it resolves the author's own label-passing issue.
public sealed class WorkItemRefresher : IWorkItemRefresher
{
    private readonly IReadOnlyList<string> _activeLabelFilter;
    private readonly PullRequestCheckTracker _checkTracker;
    private readonly IIssueDetailFetcher _issueDetailFetcher;
    private readonly ILogger<WorkItemRefresher> _logger;
    private readonly INotificationStateTracker _notificationStateTracker;
    private readonly GitHubOptions _options;
    private readonly IPullRequestDetailFetcher _pullRequestDetailFetcher;
    private readonly TimeProvider _timeProvider;

    public WorkItemRefresher(
        IPullRequestDetailFetcher pullRequestDetailFetcher,
        IIssueDetailFetcher issueDetailFetcher,
        INotificationStateTracker notificationStateTracker,
        PullRequestCheckTracker checkTracker,
        IOptions<GitHubOptions> options,
        TimeProvider timeProvider,
        ILogger<WorkItemRefresher> logger
    )
    {
        this._pullRequestDetailFetcher = pullRequestDetailFetcher;
        this._issueDetailFetcher = issueDetailFetcher;
        this._notificationStateTracker = notificationStateTracker;
        this._checkTracker = checkTracker;
        this._options = options.Value;
        this._activeLabelFilter = LinkedIssueTrust.ActiveLabelFilter(this._options.Filter.LabelFilter);
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    public ValueTask<bool> RefreshPullRequestAsync(string repo, int number, CancellationToken cancellationToken)
    {
        GitHubNotification notification = SyntheticNotifications.BuildPullRequestApiNotification(
            idPrefix: "refresh",
            repo: repo,
            number: number
        );

        return this.RefreshPullRequestAsync(notification: notification, cancellationToken: cancellationToken);
    }

    public async ValueTask<bool> RefreshPullRequestAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    )
    {
        PullRequestDetails? details = await this._pullRequestDetailFetcher.FetchAsync(
            notification: notification,
            cancellationToken: cancellationToken
        );

        if (details is null)
        {
            return false;
        }

        string repo = notification.Repository.FullName;
        IReadOnlyList<BotPrRule> rules = this._options.Filter.PullRequests.AdoptionRules;
        bool isAdopted = PullRequestAdoption.IsAdopted(rules: rules, author: details.Author, headRef: details.HeadRef);
        bool ownLabelsPass = LinkedIssueTrust.PassesLabelFilter(details.Labels, this._activeLabelFilter);
        LinkedItem? trustedLinkedIssue = isAdopted || ownLabelsPass ? null : this.FindTrustedLinkedIssue(details);

        if (!isAdopted && !ownLabelsPass && trustedLinkedIssue is null)
        {
            this._checkTracker.Remove(repo: repo, number: details.Number);

            return false;
        }

        WorkPriority priority = LinkedIssueTrust.ElevatePriority(
            PullRequestAdoption.CalculatePriority(
                rules: rules,
                author: details.Author,
                headRef: details.HeadRef,
                createdAt: details.CreatedAt,
                labelPriority: LabelParser.ParsePriority(details.Labels),
                now: this._timeProvider.GetUtcNow()
            ),
            trustedLinkedIssue
        );

        bool isOnHold = LabelParser.IsOnHold(labels: details.Labels, noWorkFilter: this._options.Filter.NoWorkFilter);

        await this._notificationStateTracker.UpdateStateAsync(
            notification: notification,
            details: details,
            priority: priority,
            isOnHold: isOnHold,
            // Without the head branch the adoption rules cannot be evaluated, so keep the stored flag.
            isAdopted: details.HeadRef is null ? null : isAdopted,
            cancellationToken: cancellationToken
        );

        this._checkTracker.RecordDetail(repo: repo, number: details.Number, details: details, isOnHold: isOnHold);
        this._logger.LogRefreshedPullRequest(repo: repo, number: details.Number, isOnHold: isOnHold);

        return true;
    }

    public async ValueTask<bool> RefreshIssueAsync(GitHubNotification notification, CancellationToken cancellationToken)
    {
        IssueDetails? details = await this._issueDetailFetcher.FetchAsync(
            notification: notification,
            cancellationToken: cancellationToken
        );

        if (details is null || !LinkedIssueTrust.PassesLabelFilter(details.Labels, this._activeLabelFilter))
        {
            return false;
        }

        bool isOnHold = LabelParser.IsOnHold(labels: details.Labels, noWorkFilter: this._options.Filter.NoWorkFilter);

        await this._notificationStateTracker.UpdateStateAsync(
            notification: notification,
            details: details,
            priority: LabelParser.ParsePriority(details.Labels),
            isOnHold: isOnHold,
            cancellationToken: cancellationToken
        );

        this._logger.LogRefreshedIssue(
            repo: notification.Repository.FullName,
            number: details.Number,
            isOnHold: isOnHold
        );

        return true;
    }

    private LinkedItem? FindTrustedLinkedIssue(PullRequestDetails details)
    {
        return LinkedIssueTrust.FindTrustedLinkedIssue(
            author: details.Author,
            commitAuthors: details.CommitAuthors,
            linkedItems: details.LinkedItems,
            activeLabelFilter: this._activeLabelFilter
        );
    }
}
