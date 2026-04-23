using System;
using System.Collections.Generic;
using System.Linq;

namespace Credfeto.Dispatcher.GitHub.Helpers;

/// <summary>
/// Helper class to determine OnHold status based on labels and configuration.
/// </summary>
public static class OnHoldHelper
{
    /// <summary>
    /// Determines if an item is on hold based on its labels and the NoWorkFilter configuration.
    /// </summary>
    /// <param name="labels">The collection of labels on the item.</param>
    /// <param name="noWorkFilters">The configured NoWorkFilter labels (case-insensitive).</param>
    /// <returns>True if the item has any label matching the NoWorkFilter, false otherwise.</returns>
    public static bool IsOnHold(IReadOnlyList<string> labels, IReadOnlyList<string> noWorkFilters)
    {
        if (labels == null || labels.Count == 0 || noWorkFilters == null || noWorkFilters.Count == 0)
        {
            return false;
        }

        return labels.Any(label => noWorkFilters.Any(filter => string.Equals(a: label, b: filter, comparisonType: StringComparison.OrdinalIgnoreCase)));
    }
}

