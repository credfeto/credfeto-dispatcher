using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[JsonSerializable(typeof(ApiNotification[]))]
[JsonSerializable(typeof(ApiPullRequest))]
[JsonSerializable(typeof(ApiPullRequestReview[]))]
[JsonSerializable(typeof(ApiIssueComment[]))]
[JsonSerializable(typeof(ApiWorkflowRunsResponse))]
[JsonSerializable(typeof(ApiIssue))]
[JsonSerializable(typeof(ApiRequiredStatusChecks))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class NotificationSerializerContext : JsonSerializerContext;
