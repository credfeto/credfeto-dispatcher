using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
    private const string E_TAG_KEY = "github.notifications";
    private static readonly Uri NotificationsRelativeUri = new(uriString: "notifications", uriKind: UriKind.Relative);

    private readonly IETagStore _eTagStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationPoller> _logger;

    public NotificationPoller(
        IHttpClientFactory httpClientFactory,
        IETagStore eTagStore,
        ILogger<NotificationPoller> logger
    )
    {
        this._httpClientFactory = httpClientFactory;
        this._eTagStore = eTagStore;
        this._logger = logger;
    }

    public async ValueTask<NotificationPollResult> PollAsync(CancellationToken cancellationToken)
    {
        string? eTag = await this._eTagStore.GetETagAsync(key: E_TAG_KEY, cancellationToken: cancellationToken);

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
        using HttpResponseMessage response = await httpClient.SendAsync(
            request: request,
            cancellationToken: cancellationToken
        );

        return await this.ProcessResponseAsync(response: response, cancellationToken: cancellationToken);
    }

    public ValueTask CommitETagAsync(string candidateETag, CancellationToken cancellationToken)
    {
        return this._eTagStore.SaveETagAsync(key: E_TAG_KEY, eTag: candidateETag, cancellationToken: cancellationToken);
    }

    private static HttpRequestMessage BuildRequest(string? eTag)
    {
        HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: NotificationsRelativeUri);

        ETagHeaderUtility.ApplyIfNoneMatch(request: request, eTag: eTag);

        return request;
    }

    private async ValueTask<NotificationPollResult> ProcessResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            this._logger.LogPollNotModified();

            return new NotificationPollResult(Notifications: [], CandidateETag: null);
        }

        _ = response.EnsureSuccessStatusCode();

        string? candidateETag = ETagHeaderUtility.ExtractETag(response.Headers);

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiNotification[] apiNotifications =
            JsonSerializer.Deserialize(
                json: json,
                jsonTypeInfo: NotificationSerializerContext.Default.ApiNotificationArray
            ) ?? [];

        List<GitHubNotification> notifications = new(apiNotifications.Length);

        foreach (ApiNotification n in apiNotifications)
        {
            GitHubNotification notification = MapNotification(n);
            this._logger.LogNotificationReceived(
                notificationId: notification.Id,
                reason: notification.Reason,
                repository: notification.Repository.FullName,
                title: notification.Subject.Title
            );
            notifications.Add(notification);
        }

        this._logger.LogPollNotificationsReceived(count: notifications.Count);

        return new NotificationPollResult(Notifications: notifications, CandidateETag: candidateETag);
    }

    private static GitHubNotification MapNotification(ApiNotification n)
    {
        return new GitHubNotification(
            Id: n.Id,
            Reason: n.Reason,
            Subject: new NotificationSubject(
                Title: n.Subject.Title,
                Url: n.Subject.Url is not null ? new Uri(n.Subject.Url) : null,
                Type: n.Subject.Type
            ),
            Repository: new NotificationRepository(FullName: n.Repository.FullName, Url: new Uri(n.Repository.HtmlUrl)),
            UpdatedAt: n.UpdatedAt,
            Unread: n.Unread
        );
    }
}
