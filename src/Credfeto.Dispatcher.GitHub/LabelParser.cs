using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub;

public static class LabelParser
{
    public static WorkPriority ParsePriority(IReadOnlyList<string> labels)
    {
        return labels.Select(ParseSingleLabel).DefaultIfEmpty(WorkPriority.Unknown).Max();
    }

    private static WorkPriority ParseSingleLabel(string label)
    {
        if (label.Contains(value: "security", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return WorkPriority.Security;
        }

        if (label.Contains(value: "urgent", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return WorkPriority.Urgent;
        }

        if (label.Contains(value: "high", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return WorkPriority.High;
        }

        if (label.Contains(value: "medium", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return WorkPriority.Medium;
        }

        if (label.Contains(value: "low", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return WorkPriority.Low;
        }

        return WorkPriority.Unknown;
    }

    public static bool IsOnHold(IReadOnlyList<string> labels, IReadOnlyList<string> noWorkFilter)
    {
        return labels.Any(label => noWorkFilter.Any(filter => FuzzyEquals(label, filter)));
    }

    internal static bool FuzzyEquals(string a, string b)
    {
        return string.Equals(
            a: Normalize(a),
            b: Normalize(b),
            comparisonType: StringComparison.OrdinalIgnoreCase
        );
    }

    private static string Normalize(string value)
    {
        return value
            .Replace(
                oldValue: "-",
                newValue: string.Empty,
                comparisonType: StringComparison.Ordinal
            )
            .Replace(
                oldValue: " ",
                newValue: string.Empty,
                comparisonType: StringComparison.Ordinal
            )
            .Replace(
                oldValue: "_",
                newValue: string.Empty,
                comparisonType: StringComparison.Ordinal
            );
    }
}
