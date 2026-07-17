using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.Storage;

internal static class NotificationDetailMapping
{
    private static readonly ImmutableHashSet<string> FailedConclusions = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "failure",
        "error",
        "timed_out",
        "action_required"
    );

    public static int? ExtractPrNumber(Uri? linkedPullRequestUrl)
    {
        if (linkedPullRequestUrl is null)
        {
            return null;
        }

        string[] segments = linkedPullRequestUrl.AbsolutePath.TrimEnd('/').Split('/');

        return int.TryParse(
            segments[^1],
            style: NumberStyles.Integer,
            provider: CultureInfo.InvariantCulture,
            out int number
        )
            ? number
            : null;
    }

    public static string? ComputeReviewDecision(IReadOnlyList<PullRequestReview> reviews)
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

    public static int CountFailedChecks(IReadOnlyList<PullRequestRun> runs)
    {
        return runs.Count(r => r.Conclusion is not null && FailedConclusions.Contains(r.Conclusion));
    }

    public static string? BuildFailedCheckNames(IReadOnlyList<PullRequestRun> runs)
    {
        string[] failed =
        [
            .. runs.Where(r => r.Conclusion is not null && FailedConclusions.Contains(r.Conclusion))
                .Select(r => r.Name),
        ];

        return failed.Length > 0 ? string.Join(separator: ',', failed) : null;
    }

    public static string? BuildFailedCheckSha(IReadOnlyList<PullRequestRun> runs)
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
