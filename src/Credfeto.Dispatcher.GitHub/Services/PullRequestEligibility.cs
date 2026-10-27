using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Services;

// CommitAuthors is only known when the PR had to be enriched to decide eligibility; otherwise it is empty
// (unknown), so the store keeps the commit authors it already has.
[DebuggerDisplay("Allowed: {Allowed}, CommitAuthors: {CommitAuthors.Count}")]
internal readonly record struct PullRequestEligibility(
    bool Allowed,
    IReadOnlyList<LinkedItem> LinkedItems,
    IReadOnlyList<string> CommitAuthors,
    LinkedItem? TrustedLinkedIssue
)
{
    public static PullRequestEligibility Rejected =>
        new(Allowed: false, LinkedItems: [], CommitAuthors: [], TrustedLinkedIssue: null);

    public static PullRequestEligibility AllowedWithoutEnrichment =>
        new(Allowed: true, LinkedItems: [], CommitAuthors: [], TrustedLinkedIssue: null);

    public static PullRequestEligibility Trusted(PullRequestDetails enriched, LinkedItem trustedLinkedIssue)
    {
        return new(
            Allowed: true,
            LinkedItems: enriched.LinkedItems,
            CommitAuthors: enriched.CommitAuthors,
            TrustedLinkedIssue: trustedLinkedIssue
        );
    }
}
