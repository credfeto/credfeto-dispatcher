using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[JsonSerializable(typeof(ApiEvent[]))]
[JsonSerializable(typeof(ApiNotification[]))]
[JsonSerializable(typeof(ApiPullRequest))]
[JsonSerializable(typeof(ApiPullRequest[]))]
[JsonSerializable(typeof(ApiPullRequestReview[]))]
[JsonSerializable(typeof(ApiIssueComment[]))]
[JsonSerializable(typeof(ApiWorkflowRunsResponse))]
[JsonSerializable(typeof(ApiIssue))]
[JsonSerializable(typeof(ApiIssue[]))]
[JsonSerializable(typeof(ApiIssuePullRequest))]
[JsonSerializable(typeof(ApiUserRepo[]))]
[JsonSerializable(typeof(ApiRequiredStatusChecks))]
[JsonSerializable(typeof(GraphQlRequest))]
[JsonSerializable(typeof(GraphQlResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class NotificationSerializerContext : JsonSerializerContext;
