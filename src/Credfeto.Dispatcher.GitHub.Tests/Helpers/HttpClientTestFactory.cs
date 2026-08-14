using System;
using System.Net;
using System.Net.Http;

namespace Credfeto.Dispatcher.GitHub.Tests.Helpers;

internal static class HttpClientTestFactory
{
    public static HttpClient Create(HttpStatusCode statusCode, string? content = null, string? linkUrl = null)
    {
        return CreateWithHandler(statusCode: statusCode, content: content, linkUrl: linkUrl).Client;
    }

    public static (HttpClient Client, FixedResponseHandler Handler) CreateWithHandler(
        HttpStatusCode statusCode,
        string? content = null,
        string? linkUrl = null
    )
    {
        FixedResponseHandler? handler = new(statusCode: statusCode, content: content, linkUrl: linkUrl);

        try
        {
            HttpClient client = new(handler: handler, disposeHandler: true)
            {
                BaseAddress = new Uri("https://api.github.com/"),
            };
            (HttpClient Client, FixedResponseHandler Handler) result = (client, handler);
            handler = null;

            return result;
        }
        finally
        {
            handler?.Dispose();
        }
    }
}
