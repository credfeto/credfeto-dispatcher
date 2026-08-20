using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dispatcher.GitHub.Tests.Helpers;

internal sealed class FixedResponseHandler : HttpMessageHandler
{
    private readonly string? _content;
    private readonly string? _eTag;
    private readonly string? _linkUrl;
    private readonly int? _pollIntervalSeconds;
    private readonly HttpStatusCode _statusCode;

    public FixedResponseHandler(
        HttpStatusCode statusCode,
        string? content = null,
        string? eTag = null,
        string? linkUrl = null,
        int? pollIntervalSeconds = null
    )
    {
        this._statusCode = statusCode;
        this._content = content;
        this._eTag = eTag;
        this._linkUrl = linkUrl;
        this._pollIntervalSeconds = pollIntervalSeconds;
    }

    public Uri? LastRequestUri { get; private set; }

    public string? LastRequestIfNoneMatch { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        this.LastRequestUri = request.RequestUri;
        this.LastRequestIfNoneMatch = request.Headers.IfNoneMatch.Select(t => t.Tag).FirstOrDefault();

        HttpResponseMessage response = new(this._statusCode);

        if (this._content is not null)
        {
            response.Content = new StringContent(
                content: this._content,
                encoding: Encoding.UTF8,
                mediaType: "application/json"
            );
        }

        if (this._eTag is not null)
        {
            response.Headers.ETag = new EntityTagHeaderValue(this._eTag);
        }

        if (this._linkUrl is not null)
        {
            response.Headers.Add(name: "Link", value: $"<{this._linkUrl}>; rel=\"next\"");
        }

        if (this._pollIntervalSeconds is not null)
        {
            response.Headers.Add(
                name: "X-Poll-Interval",
                value: this._pollIntervalSeconds.Value.ToString(CultureInfo.InvariantCulture)
            );
        }

        return Task.FromResult(response);
    }
}
