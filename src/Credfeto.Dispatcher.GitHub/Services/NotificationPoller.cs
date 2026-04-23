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
using Credfeto.Dispatcher.GitHub.Models;
using Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class NotificationPoller : INotificationPoller
{
    private const string ETagKey = "github.notifications";
    private static readonly Uri NotificationsRelativeUri = new(uriString: "notifications", uriKind: UriKind.Relative);

    private readonly IETagStore _eTagStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationPoller> _logger;

    public NotificationPoller(IHttpClientFactory httpClientFactory, IETagStore eTagStore, ILogger<NotificationPoller> logger)
    {
        this._httpClientFactory = httpClientFactory;
        this._eTagStore = eTagStore;
        this._logger = logger;
    }

    public async ValueTask<IReadOnlyList<GitHubNotification>> PollAsync(CancellationToken cancellationToken)
    {
        string? eTag = await this._eTagStore.GetETagAsync(key: ETagKey, cancellationToken: cancellationToken);

        if (eTag is null)
        {
            this._logger.LogPollingFirstCall();
        }
        else
        {
            this._logger.LogPollingWithETag(eTag: eTag);
        }

        HttpClient httpClient = this._httpClientFactory.CreateClient("GitHub");

        using HttpRequestMessage request = BuildRequest(eTag);
        using HttpResponseMessage response = await httpClient.SendAsync(request: request, cancellationToken: cancellationToken);

        return await this.ProcessResponseAsync(response: response, cancellationToken: cancellationToken);
    }

    private static HttpRequestMessage BuildRequest(string? eTag)
    {
        HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: NotificationsRelativeUri);

        if (eTag is not null)
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(eTag));
        }

        return request;
    }

    private async ValueTask<IReadOnlyList<GitHubNotification>> ProcessResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            this._logger.LogPollNotModified();

            return [];
        }

        _ = response.EnsureSuccessStatusCode();

        if (response.Headers.ETag is not null)
        {
            await this._eTagStore.SaveETagAsync(key: ETagKey, eTag: response.Headers.ETag.Tag, cancellationToken: cancellationToken);
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiNotification[]? apiNotifications = JsonSerializer.Deserialize(json: json, jsonTypeInfo: NotificationSerializerContext.Default.ApiNotificationArray);

        if (apiNotifications is null)
        {
            this._logger.LogPollNotificationsReceived(count: 0);

            return [];
        }

        List<GitHubNotification> notifications = new(apiNotifications.Length);

        foreach (ApiNotification n in apiNotifications)
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

        return notifications;
    }
}
