using System.Text.Json;
using System.Text.Json.Serialization;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.Storage.Helpers;

/// <summary>
/// Helper class for serializing and deserializing notification details to JSON.
/// </summary>
public static class NotificationStateSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes PullRequestDetails to JSON string.
    /// </summary>
    public static string SerializePullRequest(PullRequestDetails details)
    {
        return JsonSerializer.Serialize(
            new
            {
                details.Number,
                details.Title,
                details.Body,
                Status = details.Status.GetName(),
                Priority = details.Priority.GetName(),
                details.OnHold,
                details.Assignees,
                details.Labels,
                details.LinkedItems,
                CommentCount = details.Comments.Count,
                ReviewCount = details.Reviews.Count,
                RunCount = details.Runs.Count
            },
            Options);
    }

    /// <summary>
    /// Serializes IssueDetails to JSON string.
    /// </summary>
    public static string SerializeIssue(IssueDetails details)
    {
        return JsonSerializer.Serialize(
            new
            {
                details.Number,
                details.Title,
                Status = details.Status.GetName(),
                Priority = details.Priority.GetName(),
                details.OnHold
            },
            Options);
    }
}

