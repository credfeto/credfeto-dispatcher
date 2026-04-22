using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class GitHubNotificationPoller : IGitHubNotificationPoller
{
    private const string GitHubApiBase = "https://api.github.com";
    private readonly HttpClient _httpClient;
    private string? _eTag;
    private IReadOnlyList<GitHubNotification> _lastResult = [];

    public GitHubNotificationPoller(HttpClient httpClient)
    {
        this._httpClient = httpClient;
    }

    public async ValueTask<IReadOnlyList<GitHubNotification>> PollAsync(CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: new Uri($"{GitHubApiBase}/notifications"));

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add(name: "X-GitHub-Api-Version", value: "2022-11-28");

        if (this._eTag is not null)
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(this._eTag));
        }

        using HttpResponseMessage response = await this._httpClient.SendAsync(request: request, cancellationToken: cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return this._lastResult;
        }

        response.EnsureSuccessStatusCode();

        if (response.Headers.ETag is not null)
        {
            this._eTag = response.Headers.ETag.Tag;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        GitHubApiNotification[]? apiNotifications = JsonSerializer.Deserialize(json: json, jsonTypeInfo: GitHubNotificationContext.Default.GitHubApiNotificationArray);

        if (apiNotifications is null)
        {
            this._lastResult = [];

            return this._lastResult;
        }

        List<GitHubNotification> notifications = new(apiNotifications.Length);

        foreach (GitHubApiNotification n in apiNotifications)
        {
            notifications.Add(
                new GitHubNotification(
                    Id: n.Id,
                    Reason: n.Reason,
                    Subject: new NotificationSubject(Title: n.Subject.Title, Url: n.Subject.Url ?? string.Empty, Type: n.Subject.Type),
                    Repository: new NotificationRepository(FullName: n.Repository.FullName, Url: n.Repository.HtmlUrl),
                    UpdatedAt: n.UpdatedAt,
                    Unread: n.Unread
                )
            );
        }

        this._lastResult = notifications;

        return this._lastResult;
    }
}

internal sealed record GitHubApiNotification(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("subject")] GitHubApiSubject Subject,
    [property: JsonPropertyName("repository")] GitHubApiRepository Repository,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("unread")] bool Unread
);

internal sealed record GitHubApiSubject(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("type")] string Type
);

internal sealed record GitHubApiRepository(
    [property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("html_url")] string HtmlUrl
);

[JsonSerializable(typeof(GitHubApiNotification[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class GitHubNotificationContext : JsonSerializerContext;
