using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Credfeto.Dispatcher.GitHub.Services;

internal static class ETagHeaderUtility
{
    private const string POLL_INTERVAL_HEADER = "X-Poll-Interval";
    private const int MAX_POLL_INTERVAL_SECONDS = 3600;

    internal static bool IsUsableETag([NotNullWhen(returnValue: true)] string? eTag)
    {
        return !string.IsNullOrEmpty(eTag) && !string.Equals(eTag, "\"\"", StringComparison.Ordinal);
    }

    internal static void ApplyIfNoneMatch(HttpRequestMessage request, string? eTag)
    {
        if (IsUsableETag(eTag))
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(eTag));
        }
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
                return Math.Min(seconds, MAX_POLL_INTERVAL_SECONDS);
            }
        }

        return null;
    }

    internal static int? MaxPollIntervalSeconds(int? left, int? right)
    {
        return left is null || right is null ? left ?? right : Math.Max(left.Value, right.Value);
    }
}
