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
    private const string FIRST_PAGE_URL = "notifications?per_page=100";

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

        using HttpResponseMessage firstResponse = await SendAsync(
            httpClient: httpClient,
            url: FIRST_PAGE_URL,
            eTag: eTag,
            cancellationToken: cancellationToken
        );

        if (firstResponse.StatusCode == HttpStatusCode.NotModified)
        {
            this._logger.LogPollNotModified();

            return new NotificationPollResult(Notifications: [], CandidateETag: null);
        }

        _ = firstResponse.EnsureSuccessStatusCode();

        // The ETag describes the first page only; it is handed back as a candidate only once every page
        // has been read, so a failure part way through leaves the stored ETag untouched and the next
        // poll re-fetches everything.
        string? candidateETag = ETagHeaderUtility.ExtractETag(firstResponse.Headers);

        List<GitHubNotification> notifications = [];
        string? nextUrl = await this.ReadPageAsync(
            response: firstResponse,
            notifications: notifications,
            cancellationToken: cancellationToken
        );

        while (nextUrl is not null)
        {
            nextUrl = await this.ReadNextPageAsync(
                httpClient: httpClient,
                url: nextUrl,
                notifications: notifications,
                cancellationToken: cancellationToken
            );
        }

        this._logger.LogPollNotificationsReceived(count: notifications.Count);

        return new NotificationPollResult(Notifications: notifications, CandidateETag: candidateETag);
    }

    public ValueTask CommitETagAsync(string candidateETag, CancellationToken cancellationToken)
    {
        return this._eTagStore.SaveETagAsync(key: E_TAG_KEY, eTag: candidateETag, cancellationToken: cancellationToken);
    }

    private static async ValueTask<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        string url,
        string? eTag,
        CancellationToken cancellationToken
    )
    {
        using HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: url);

        ETagHeaderUtility.ApplyIfNoneMatch(request: request, eTag: eTag);

        return await httpClient.SendAsync(request: request, cancellationToken: cancellationToken);
    }

    private async ValueTask<string?> ReadNextPageAsync(
        HttpClient httpClient,
        string url,
        List<GitHubNotification> notifications,
        CancellationToken cancellationToken
    )
    {
        // Later pages are requested unconditionally: the stored ETag belongs to the first page.
        using HttpResponseMessage response = await SendAsync(
            httpClient: httpClient,
            url: url,
            eTag: null,
            cancellationToken: cancellationToken
        );

        _ = response.EnsureSuccessStatusCode();

        return await this.ReadPageAsync(
            response: response,
            notifications: notifications,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask<string?> ReadPageAsync(
        HttpResponseMessage response,
        List<GitHubNotification> notifications,
        CancellationToken cancellationToken
    )
    {
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiNotification[] apiNotifications =
            JsonSerializer.Deserialize(
                json: json,
                jsonTypeInfo: NotificationSerializerContext.Default.ApiNotificationArray
            ) ?? [];

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

        return GitHubRepoHelper.ParseNextLink(response.Headers);
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
