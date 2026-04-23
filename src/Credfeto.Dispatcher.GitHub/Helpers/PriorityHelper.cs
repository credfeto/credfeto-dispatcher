using System;
using System.Collections.Generic;

namespace Credfeto.Dispatcher.GitHub.Helpers;

/// <summary>
/// Helper class to determine priority from labels.
/// </summary>
public static class PriorityHelper
{
    /// <summary>
    /// Determines the priority based on labels.
    /// Priority labels are checked in order: Urgent, High, Medium, Low.
    /// First match wins.
    /// </summary>
    /// <param name="labels">The collection of labels.</param>
    /// <returns>The priority level: Urgent, High, Medium, Low, or Unknown.</returns>
    public static string DeterminePriority(IReadOnlyList<string> labels)
    {
        if (labels == null || labels.Count == 0)
        {
            return "Unknown";
        }

        foreach (string label in labels)
        {
            if (string.Equals(a: label, b: "urgent", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                return "Urgent";
            }

            if (string.Equals(a: label, b: "high", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                return "High";
            }

            if (string.Equals(a: label, b: "medium", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                return "Medium";
            }

            if (string.Equals(a: label, b: "low", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                return "Low";
            }
        }

        return "Unknown";
    }
}

