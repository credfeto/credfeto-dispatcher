using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub;

internal static class LabelParser
{
    private static readonly IReadOnlyList<(string Keyword, WorkPriority Priority)> PriorityKeywordsHighestFirst =
    [
        ("security", WorkPriority.SECURITY),
        ("urgent", WorkPriority.URGENT),
        ("high", WorkPriority.HIGH),
        ("medium", WorkPriority.MEDIUM),
        ("low", WorkPriority.LOW),
    ];

    public static WorkPriority ParsePriority(IReadOnlyList<string> labels)
    {
        foreach ((string keyword, WorkPriority priority) in PriorityKeywordsHighestFirst)
        {
            if (labels.Any(label => label.Contains(value: keyword, comparisonType: StringComparison.OrdinalIgnoreCase)))
            {
                return priority;
            }
        }

        return WorkPriority.UNKNOWN;
    }

    public static bool IsOnHold(IReadOnlyList<string> labels, IReadOnlyList<string> noWorkFilter)
    {
        return labels.Any(label => noWorkFilter.Any(filter => FuzzyEquals(label, filter)));
    }

    internal static bool FuzzyEquals(string a, string b)
    {
        return string.Equals(a: Normalize(a), b: Normalize(b), comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return value
            .Replace(oldValue: "-", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: " ", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "_", newValue: string.Empty, comparisonType: StringComparison.Ordinal);
    }
}
