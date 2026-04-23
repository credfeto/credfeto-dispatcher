using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dispatcher.GitHub.Tests.Helpers;

internal sealed class FixedResponseHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string? _content;

    public FixedResponseHandler(HttpStatusCode statusCode, string? content = null)
    {
        this._statusCode = statusCode;
        this._content = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = new(this._statusCode);

        if (this._content is not null)
        {
            response.Content = new StringContent(content: this._content, encoding: Encoding.UTF8, mediaType: "application/json");
        }

        return Task.FromResult(response);
    }
}
