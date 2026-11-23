using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Credfeto.Dispatcher.GitHub.Services;

internal static class CheckRunSignature
{
    private const string COMPLETED_STATUS = "completed";

    // Matches the conclusions the store counts as failed checks.
    private static readonly ImmutableHashSet<string> FailedConclusions = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "failure",
        "error",
        "timed_out",
        "action_required"
    );

    public static (string Signature, bool Settled) Compute(
        IReadOnlyList<(string Name, string Status, string? Conclusion)> runs
    )
    {
        string signature = string.Join(
            separator: '\n',
            values: runs.Select(static r => $"{r.Name}|{r.Status}|{r.Conclusion}").Order(StringComparer.Ordinal)
        );

        bool settled =
            runs.Count != 0
            && runs.All(static r =>
                StringComparer.OrdinalIgnoreCase.Equals(r.Status, COMPLETED_STATUS)
                && (r.Conclusion is null || !FailedConclusions.Contains(r.Conclusion))
            );

        return (signature, settled);
    }
}
