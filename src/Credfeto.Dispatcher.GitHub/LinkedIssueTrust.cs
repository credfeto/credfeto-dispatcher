using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub;

internal static class LinkedIssueTrust
{
    public static WorkPriority ElevatePriority(WorkPriority priority, LinkedItem? trustedLinkedIssue)
    {
        if (trustedLinkedIssue is null)
        {
            return priority;
        }

        WorkPriority linkedIssuePriority = LabelParser.ParsePriority(trustedLinkedIssue.Labels);

        return linkedIssuePriority > priority ? linkedIssuePriority : priority;
    }

    public static bool PassesLabelFilter(IReadOnlyList<string> labels, IReadOnlyList<string> labelFilter)
    {
        List<string> filters = [.. labelFilter.Where(static f => !string.IsNullOrWhiteSpace(f))];

        if (filters.Count == 0)
        {
            return true;
        }

        return labels.Any(label => filters.Exists(filter => LabelParser.FuzzyEquals(label, filter)));
    }

    // A PR that fails its own label filter is still trusted when it is unambiguously "the author's own
    // branch resolving the author's own issue": the author wrote every commit on the branch, the author
    // is assigned to a closing-referenced issue, and that issue itself passes the label filter.
    public static LinkedItem? FindTrustedLinkedIssue(
        string? author,
        IReadOnlyList<string> commitAuthors,
        IReadOnlyList<LinkedItem> linkedItems,
        IReadOnlyList<string> labelFilter
    )
    {
        if (string.IsNullOrEmpty(author) || commitAuthors.Count == 0)
        {
            return null;
        }

        if (
            !commitAuthors.All(commitAuthor =>
                string.Equals(a: commitAuthor, b: author, comparisonType: StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return null;
        }

        return linkedItems.FirstOrDefault(issue =>
            issue.Assignees.Any(assignee =>
                string.Equals(a: assignee, b: author, comparisonType: StringComparison.OrdinalIgnoreCase)
            ) && PassesLabelFilter(issue.Labels, labelFilter)
        );
    }
}
