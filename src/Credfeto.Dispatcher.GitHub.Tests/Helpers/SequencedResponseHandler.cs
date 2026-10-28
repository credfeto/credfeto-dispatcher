using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dispatcher.GitHub.Tests.Helpers;

internal sealed class SequencedResponseHandler : HttpMessageHandler
{
    private readonly List<string?> _requestIfNoneMatches = [];
    private readonly List<Uri?> _requestUris = [];
    private readonly IReadOnlyList<SequencedResponse> _responses;

    public SequencedResponseHandler(params SequencedResponse[] responses)
    {
        this._responses = responses;
    }

    // Captured as plain values at send time because the request message is disposed once sent.
    public IReadOnlyList<Uri?> RequestUris => this._requestUris;

    public IReadOnlyList<string?> RequestIfNoneMatches => this._requestIfNoneMatches;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        int index = this._requestUris.Count;

        this._requestUris.Add(request.RequestUri);
        this._requestIfNoneMatches.Add(request.Headers.IfNoneMatch.Select(t => t.Tag).FirstOrDefault());

        if (index >= this._responses.Count)
        {
            throw new InvalidOperationException($"Unexpected request {index + 1}: {request.RequestUri}");
        }

        return Task.FromResult(BuildResponse(this._responses[index]));
    }

    private static HttpResponseMessage BuildResponse(SequencedResponse spec)
    {
        HttpResponseMessage response = new(spec.StatusCode);

        if (spec.Content is not null)
        {
            response.Content = new StringContent(
                content: spec.Content,
                encoding: Encoding.UTF8,
                mediaType: "application/json"
            );
        }

        if (spec.ETag is not null)
        {
            response.Headers.ETag = new EntityTagHeaderValue(spec.ETag);
        }

        if (spec.LinkUrl is not null)
        {
            response.Headers.Add(name: "Link", value: $"<{spec.LinkUrl}>; rel=\"next\"");
        }

        return response;
    }
}
