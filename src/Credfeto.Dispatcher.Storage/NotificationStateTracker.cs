using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Database;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.Storage.Database;

namespace Credfeto.Dispatcher.Storage;

public sealed class NotificationStateTracker : INotificationStateTracker
{
    private const string CLOSED_STATUS = "Closed";

    private static readonly ImmutableHashSet<string> FailedConclusions = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "failure",
        "error",
        "timed_out",
        "action_required"
    );

    private readonly IDatabase _database;
    private readonly TimeProvider _timeProvider;

    public NotificationStateTracker(IDatabase database, TimeProvider timeProvider)
    {
        this._database = database;
        this._timeProvider = timeProvider;
    }

    public ValueTask<bool> ShouldSkipAsync(
        GitHubNotification notification,
        PullRequestDetails details,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(IsClosedStatus(details.Status));
    }

    public ValueTask UpdateStateAsync(
        GitHubNotification notification,
        PullRequestDetails details,
        WorkPriority priority,
        bool isOnHold,
        CancellationToken cancellationToken
    )
    {
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        return this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.PullRequests_UpsertAsync(
                    connection: c,
                    repository: notification.Repository.FullName,
                    id: details.Number,
                    status: details.Status,
                    priority: (int)priority,
                    isOnHold: isOnHold,
                    commentCount: details.Comments.Count,
                    reviewDecision: ComputeReviewDecision(details.Reviews),
                    failedCheckCount: CountFailedChecks(details.Runs),
                    failedCheckNames: BuildFailedCheckNames(details.Runs),
                    failedCheckSha: BuildFailedCheckSha(details.Runs),
                    author: details.Author,
                    headBranchName: details.HeadBranchName,
                    now: now,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }

    public ValueTask<bool> ShouldSkipAsync(
        GitHubNotification notification,
        IssueDetails details,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(IsClosedStatus(details.Status));
    }

    public ValueTask UpdateStateAsync(
        GitHubNotification notification,
        IssueDetails details,
        WorkPriority priority,
        bool isOnHold,
        CancellationToken cancellationToken
    )
    {
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        return this._database.ExecuteAsync(
            action: (c, ct) =>
                DispatcherDatabase.Issues_UpsertAsync(
                    connection: c,
                    repository: notification.Repository.FullName,
                    id: details.Number,
                    status: details.Status,
                    priority: (int)priority,
                    isOnHold: isOnHold,
                    linkedPrNumber: ExtractPrNumber(details.LinkedPullRequestUrl),
                    now: now,
                    cancellationToken: ct
                ),
            cancellationToken: cancellationToken
        );
    }

    private static bool IsClosedStatus(string status)
    {
        return string.Equals(a: status, b: CLOSED_STATUS, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static int? ExtractPrNumber(Uri? linkedPullRequestUrl)
    {
        if (linkedPullRequestUrl is null)
        {
            return null;
        }

        string[] segments = linkedPullRequestUrl.AbsolutePath.TrimEnd('/').Split('/');

        return int.TryParse(
            segments[^1],
            style: System.Globalization.NumberStyles.Integer,
            provider: System.Globalization.CultureInfo.InvariantCulture,
            out int number
        )
            ? number
            : null;
    }

    private static string? ComputeReviewDecision(IReadOnlyList<PullRequestReview> reviews)
    {
        if (reviews.Any(r => string.Equals(r.State, "CHANGES_REQUESTED", StringComparison.OrdinalIgnoreCase)))
        {
            return "ChangesRequested";
        }

        if (reviews.Any(r => string.Equals(r.State, "APPROVED", StringComparison.OrdinalIgnoreCase)))
        {
            return "Approved";
        }

        return null;
    }

    private static int CountFailedChecks(IReadOnlyList<PullRequestRun> runs)
    {
        return runs.Count(r => r.Conclusion is not null && FailedConclusions.Contains(r.Conclusion));
    }

    private static string? BuildFailedCheckNames(IReadOnlyList<PullRequestRun> runs)
    {
        string[] failed =
        [
            .. runs.Where(r => r.Conclusion is not null && FailedConclusions.Contains(r.Conclusion))
                .Select(r => r.Name),
        ];

        return failed.Length > 0 ? string.Join(separator: ',', failed) : null;
    }

    private static string? BuildFailedCheckSha(IReadOnlyList<PullRequestRun> runs)
    {
        string[] shas =
        [
            .. runs.Where(r => r.Conclusion is not null && FailedConclusions.Contains(r.Conclusion))
                .Select(r => r.HeadSha)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];

        return shas.Length > 0 ? shas[0] : null;
    }
}
