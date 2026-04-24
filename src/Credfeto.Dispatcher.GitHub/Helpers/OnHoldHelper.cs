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
    /// <param name="labelFilters">The configured LabelFilter labels (case-insensitive).</param>
    /// <returns>
    /// True when any NoWorkFilter label is present, or when LabelFilter is configured and none of those labels are present.
    /// </returns>
    public static bool IsOnHold(IReadOnlyList<string> labels, IReadOnlyList<string> noWorkFilters, IReadOnlyList<string> labelFilters)
    {
        bool hasNoWorkMatch = labels is not null
                              && noWorkFilters is not null
                              && labels.Any(label => noWorkFilters.Any(filter => string.Equals(a: label, b: filter, comparisonType: StringComparison.OrdinalIgnoreCase)));

        if (hasNoWorkMatch)
        {
            return true;
        }

        bool hasRequiredLabel = labels is not null
                                && labelFilters is not null
                                && labelFilters.Count > 0
                                && labels.Any(label => labelFilters.Any(filter => string.Equals(a: label, b: filter, comparisonType: StringComparison.OrdinalIgnoreCase)));

        return labelFilters is not null && labelFilters.Count > 0 && !hasRequiredLabel;
    }
}

