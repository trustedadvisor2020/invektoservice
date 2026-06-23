using System.Net;

namespace Chatinbox.Tests._Shared.Infrastructure;

/// <summary>
/// Intercepts outgoing HTTP calls and returns preconfigured responses.
/// Used to mock external APIs (Claude, OpenAI, inter-service calls).
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> Predicate, Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory)> _handlers = new();
    private readonly List<HttpRequestMessage> _capturedRequests = new();
    private readonly object _lock = new();

    public IReadOnlyList<HttpRequestMessage> CapturedRequests
    {
        get { lock (_lock) { return _capturedRequests.ToList(); } }
    }

    public void When(Func<HttpRequestMessage, bool> predicate, HttpResponseMessage response)
    {
        _handlers.Add((predicate, _ => response));
    }

    public void When(Func<HttpRequestMessage, bool> predicate, Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        _handlers.Add((predicate, responseFactory));
    }

    public void WhenUrl(string urlContains, HttpResponseMessage response)
    {
        When(r => r.RequestUri?.ToString().Contains(urlContains, StringComparison.OrdinalIgnoreCase) == true, response);
    }

    /// <summary>
    /// Register a handler that creates a fresh response per request (avoids disposed-response issues).
    /// </summary>
    public void WhenUrl(string urlContains, HttpStatusCode statusCode, string? jsonContent = null)
    {
        When(
            r => r.RequestUri?.ToString().Contains(urlContains, StringComparison.OrdinalIgnoreCase) == true,
            _ =>
            {
                var resp = new HttpResponseMessage(statusCode);
                if (jsonContent != null)
                    resp.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                return resp;
            });
    }

    public void WhenAny(HttpResponseMessage response)
    {
        When(_ => true, response);
    }

    public void Clear()
    {
        _handlers.Clear();
        lock (_lock) { _capturedRequests.Clear(); }
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_lock) { _capturedRequests.Add(request); }

        foreach (var (predicate, factory) in _handlers)
        {
            if (predicate(request))
                return Task.FromResult(factory(request));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent($"MockHttpMessageHandler: No handler matched for {request.Method} {request.RequestUri}")
        });
    }
}
