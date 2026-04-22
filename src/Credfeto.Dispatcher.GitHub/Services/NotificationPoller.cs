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

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class NotificationPoller : INotificationPoller
{
    private static readonly Uri NotificationsRelativeUri = new(uriString: "notifications", uriKind: UriKind.Relative);

    private readonly IHttpClientFactory _httpClientFactory;
    private string? _eTag;
    private IReadOnlyList<GitHubNotification> _lastResult = [];

    public NotificationPoller(IHttpClientFactory httpClientFactory)
    {
        this._httpClientFactory = httpClientFactory;
    }

    public async ValueTask<IReadOnlyList<GitHubNotification>> PollAsync(CancellationToken cancellationToken)
    {
        HttpClient httpClient = this._httpClientFactory.CreateClient("GitHub");

        using HttpRequestMessage request = this.BuildRequest();
        using HttpResponseMessage response = await httpClient.SendAsync(request: request, cancellationToken: cancellationToken);

        return await this.ProcessResponseAsync(response: response, cancellationToken: cancellationToken);
    }

    private HttpRequestMessage BuildRequest()
    {
        HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: NotificationsRelativeUri);

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
            return this._lastResult;
        }

        _ = response.EnsureSuccessStatusCode();

        if (response.Headers.ETag is not null)
        {
            this._eTag = response.Headers.ETag.Tag;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiNotification[]? apiNotifications = JsonSerializer.Deserialize(json: json, jsonTypeInfo: NotificationSerializerContext.Default.ApiNotificationArray);

        if (apiNotifications is null)
        {
            this._lastResult = [];

            return this._lastResult;
        }

        List<GitHubNotification> notifications = new(apiNotifications.Length);

        foreach (ApiNotification n in apiNotifications)
        {
            notifications.Add(
                new GitHubNotification(
                    Id: n.Id,
                    Reason: n.Reason,
                    Subject: new NotificationSubject(Title: n.Subject.Title, Url: new Uri(n.Subject.Url ?? "about:blank"), Type: n.Subject.Type),
                    Repository: new NotificationRepository(FullName: n.Repository.FullName, Url: new Uri(n.Repository.HtmlUrl)),
                    UpdatedAt: n.UpdatedAt,
                    Unread: n.Unread
                )
            );
        }

        this._lastResult = notifications;

        return this._lastResult;
    }
}
