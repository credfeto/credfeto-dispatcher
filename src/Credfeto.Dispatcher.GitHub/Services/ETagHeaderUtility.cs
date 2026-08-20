using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Headers;

namespace Credfeto.Dispatcher.GitHub.Services;

internal static class ETagHeaderUtility
{
    private const string POLL_INTERVAL_HEADER = "X-Poll-Interval";

    internal static bool IsUsableETag([NotNullWhen(returnValue: true)] string? eTag)
    {
        return !string.IsNullOrEmpty(eTag) && !string.Equals(eTag, "\"\"", StringComparison.Ordinal);
    }

    internal static string? ExtractETag(HttpResponseHeaders headers)
    {
        string? tag = headers.ETag?.Tag;

        return IsUsableETag(tag) ? tag : null;
    }

    internal static int? ExtractPollIntervalSeconds(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues(name: POLL_INTERVAL_HEADER, out IEnumerable<string>? values))
        {
            return null;
        }

        foreach (string value in values)
        {
            if (
                int.TryParse(
                    s: value,
                    style: NumberStyles.Integer,
                    provider: CultureInfo.InvariantCulture,
                    result: out int seconds
                )
                && seconds > 0
            )
            {
                return seconds;
            }
        }

        return null;
    }

    internal static int? MaxPollIntervalSeconds(int? left, int? right)
    {
        return left is null || right is null ? left ?? right : Math.Max(left.Value, right.Value);
    }
}
