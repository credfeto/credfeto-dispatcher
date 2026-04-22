using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class GitHubNotificationPoller : IGitHubNotificationPoller
{
    private const string GitHubApiBase = "https://api.github.com";
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubNotificationPoller> _logger;
    private string? _eTag;
    private IReadOnlyList<GitHubNotification> _lastResult = [];

    public GitHubNotificationPoller(HttpClient httpClient, ILogger<GitHubNotificationPoller> logger)
    {
        this._httpClient = httpClient;
        this._logger = logger;
    }

    public async ValueTask<IReadOnlyList<GitHubNotification>> PollAsync(CancellationToken cancellationToken)
    {
        if (this._eTag is null)
        {
            this._logger.LogPollingFirstCall();
        }
        else
        {
            this._logger.LogPollingWithETag(eTag: this._eTag);
        }

        HttpResponseMessage? response = null;

        try
        {
            using HttpRequestMessage request = this.BuildRequest();
            response = await this._httpClient.SendAsync(request: request, cancellationToken: cancellationToken);

            return await this.ProcessResponseAsync(response: response, cancellationToken: cancellationToken);
        }
        finally
        {
            response?.Dispose();
        }
    }

    private HttpRequestMessage BuildRequest()
    {
        HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: new Uri($"{GitHubApiBase}/notifications"));

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add(name: "X-GitHub-Api-Version", value: "2022-11-28");

        if (this._eTag is not null)
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(this._eTag));
        }

        return request;
    }

    private async ValueTask<IReadOnlyList<GitHubNotification>> ProcessResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            this._logger.LogPollNotModified();

            return this._lastResult;
        }

        _ = response.EnsureSuccessStatusCode();

        if (response.Headers.ETag is not null)
        {
            this._eTag = response.Headers.ETag.Tag;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        GitHubApiNotification[]? apiNotifications = JsonSerializer.Deserialize(json: json, jsonTypeInfo: GitHubNotificationContext.Default.GitHubApiNotificationArray);

        if (apiNotifications is null)
        {
            this._lastResult = [];
            this._logger.LogPollNotificationsReceived(count: 0);

            return this._lastResult;
        }

        List<GitHubNotification> notifications = new(apiNotifications.Length);

        foreach (GitHubApiNotification n in apiNotifications)
        {
            GitHubNotification notification = new(
                Id: n.Id,
                Reason: n.Reason,
                Subject: new NotificationSubject(Title: n.Subject.Title, Url: new Uri(n.Subject.Url ?? "about:blank"), Type: n.Subject.Type),
                Repository: new NotificationRepository(FullName: n.Repository.FullName, Url: new Uri(n.Repository.HtmlUrl)),
                UpdatedAt: n.UpdatedAt,
                Unread: n.Unread
            );

            this._logger.LogNotificationReceived(notificationId: notification.Id, reason: notification.Reason, repository: notification.Repository.FullName, title: notification.Subject.Title);

            notifications.Add(notification);
        }

        this._logger.LogPollNotificationsReceived(count: notifications.Count);

        this._lastResult = notifications;

        return this._lastResult;
    }
}
