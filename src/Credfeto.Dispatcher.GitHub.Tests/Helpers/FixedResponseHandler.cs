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
    private readonly HttpStatusCode _statusCode;

    public FixedResponseHandler(HttpStatusCode statusCode, string? content = null, string? eTag = null)
    {
        this._statusCode = statusCode;
        this._content = content;
        this._eTag = eTag;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = new(this._statusCode);

        if (this._content is not null)
        {
            response.Content = new StringContent(content: this._content, encoding: Encoding.UTF8, mediaType: "application/json");
        }

        if (this._eTag is not null)
        {
            response.Headers.ETag = new EntityTagHeaderValue(this._eTag);
        }

        return Task.FromResult(response);
    }
}
