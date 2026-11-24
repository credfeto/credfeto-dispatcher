using System;
using System.Globalization;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Services;

// What a newly created branch's name says about the work it is for.
internal static class BranchNameParser
{
    private const int MAX_ISSUE_NUMBER_DIGITS = 7;

    public static bool IsDependencyBranch(string branch)
    {
        return DependencyBranchNames.IsDependencyBranch(branch);
    }

    // The issue number in the common forms: fix/292-x, feat/6-x, 292-x, 292 and issue-292-x. Only the first path
    // segment that starts with a digit (after an optional issue- or issue_ prefix) counts, and its number must be
    // followed by the end of the segment, '-' or '_', so v2-x, 2fa-x, release/1.2.3 and 0-x give nothing.
    public static bool TryGetIssueNumber(string branch, out int number)
    {
        foreach (string segment in branch.Split(separator: '/', options: StringSplitOptions.RemoveEmptyEntries))
        {
            ReadOnlySpan<char> candidate = StripIssuePrefix(segment);

            if (candidate.Length != 0 && char.IsAsciiDigit(candidate[0]))
            {
                return TryParseLeadingNumber(candidate: candidate, number: out number);
            }
        }

        number = 0;

        return false;
    }

    private static ReadOnlySpan<char> StripIssuePrefix(string segment)
    {
        const string issuePrefix = "issue";

        if (
            segment.Length > issuePrefix.Length
            && segment.StartsWith(value: issuePrefix, comparisonType: StringComparison.OrdinalIgnoreCase)
            && segment[issuePrefix.Length] is '-' or '_'
        )
        {
            return segment.AsSpan(issuePrefix.Length + 1);
        }

        return segment.AsSpan();
    }

    private static bool TryParseLeadingNumber(in ReadOnlySpan<char> candidate, out int number)
    {
        int digits = 0;

        while (digits < candidate.Length && char.IsAsciiDigit(candidate[digits]))
        {
            ++digits;
        }

        bool wellFormed =
            candidate[0] != '0'
            && digits <= MAX_ISSUE_NUMBER_DIGITS
            && (digits == candidate.Length || candidate[digits] is '-' or '_');

        if (!wellFormed)
        {
            number = 0;

            return false;
        }

        return int.TryParse(
            s: candidate[..digits],
            style: NumberStyles.None,
            provider: CultureInfo.InvariantCulture,
            result: out number
        );
    }
}
