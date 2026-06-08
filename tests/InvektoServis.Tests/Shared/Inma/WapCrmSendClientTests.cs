using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Invekto.Shared.Contracts.Inma;
using Invekto.Shared.Contracts.Inma.Dtos;
using InvektoServis.Tests._Shared.Infrastructure;

namespace InvektoServis.Tests.Shared.Inma;

/// <summary>
/// FEAT-PROJELER / cxapi send engine — PR-2. Fake-HttpMessageHandler integration
/// tests for <see cref="WapCrmSendClient"/>. Delay/jitter are injected as no-ops so
/// the rate-limit retries run instantly and deterministically (no real sleeps).
/// </summary>
public sealed class WapCrmSendClientTests
{
    // ── Test scaffolding ──────────────────────────────────────────────

    /// <summary>A handler that records the secret header + body of every request and replies via a (request, zero-based call index) responder.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responder;
        private int _count;

        public ConcurrentQueue<CapturedRequest> Captured { get; } = new();

        public StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder) => _responder = responder;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var index = Interlocked.Increment(ref _count) - 1;
            var secret = request.Headers.TryGetValues("X-CIB-SecretKey", out var values)
                ? string.Join(",", values)
                : null;
            var body = request.Content != null ? await request.Content.ReadAsStringAsync(ct) : "";
            Captured.Enqueue(new CapturedRequest(secret, body, request.Method.Method, request.RequestUri?.ToString() ?? ""));
            return _responder(request, index); // may throw (timeout/transport simulation)
        }
    }

    private sealed record CapturedRequest(string? Secret, string Body, string Method, string Url);

    private static (WapCrmSendClient Client, HttpClient Http, StubHandler Handler) Build(
        Func<HttpRequestMessage, int, HttpResponseMessage> responder,
        WapCrmSendOptions? options = null)
    {
        var handler = new StubHandler(responder);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://cxapi.wapcrm.net/"),
            Timeout = Timeout.InfiniteTimeSpan // per-attempt timeout is enforced inside the client
        };
        var opts = options ?? new WapCrmSendOptions
        {
            TimeoutMs = 5_000,
            MaxRateLimitRetries = 3,
            BaseBackoffMs = 1,
            MaxBackoffMs = 4,
            MaxJitterMs = 0
        };
        var client = new WapCrmSendClient(
            http, opts, FakeLoggerFactory.Create("WapCrmSend.Test"),
            delay: (_, _) => Task.CompletedTask,
            jitter: _ => 0);
        return (client, http, handler);
    }

    private static WapCrmSendRequest Req(string secret = "secret-1") => new()
    {
        TenantId = 42,
        InstanceId = 15,
        UserId = 7,
        SecretKey = secret,
        ChatPhoneNumber = "905551112233",
        MessageText = "Merhaba"
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Envelope(
        bool status, string? statusCode = null, string? requestId = null, string? message = null,
        HttpStatusCode http = HttpStatusCode.OK, object? data = null)
        => Json(http, JsonSerializer.Serialize(new
        {
            status,
            message,
            statusCode,
            requestID = requestId,
            data
        }));

    private static HttpResponseMessage Redirect(int code, int? retryAfterSeconds = null)
    {
        var resp = new HttpResponseMessage((HttpStatusCode)code);
        if (retryAfterSeconds.HasValue)
            resp.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfterSeconds.Value));
        return resp;
    }

    // ── AC1/AC3: Submitted + exact wire body ──────────────────────────

    [Fact]
    public async Task Submitted_on_http200_status_true_with_correct_body_and_header()
    {
        var (client, http, handler) = Build((_, _) => Envelope(true, "200", "req-123", "ok"));

        var result = await client.SendPlainTextAsync(Req());

        result.Outcome.Should().Be(WapCrmSendOutcome.Submitted);
        result.IsSubmitted.Should().BeTrue();
        result.HttpStatusCode.Should().Be(200);
        result.ProviderStatus.Should().BeTrue();
        result.ProviderRequestId.Should().Be("req-123");
        result.ProviderMessageId.Should().BeNull(); // no 'data' in this envelope -> no wamid captured
        result.ProviderStatusCode.Should().Be("200");
        result.AttemptCount.Should().Be(1);
        result.TenantId.Should().Be(42);
        result.InstanceId.Should().Be(15);

        handler.Captured.Should().HaveCount(1);
        var cap = handler.Captured.Single();
        cap.Method.Should().Be("POST");
        cap.Url.Should().EndWith("/api/chatoperation");
        cap.Secret.Should().Be("secret-1");

        using var doc = JsonDocument.Parse(cap.Body);
        var root = doc.RootElement;
        root.GetProperty("instanceID").GetInt32().Should().Be(15);
        root.GetProperty("userID").GetInt32().Should().Be(7);
        root.GetProperty("chatPhoneNumber").GetString().Should().Be("905551112233");
        root.GetProperty("messageType").GetInt32().Should().Be(1);
        root.GetProperty("messageText").GetString().Should().Be("Merhaba");

        http.DefaultRequestHeaders.Contains("X-CIB-SecretKey").Should().BeFalse();
    }

    // ── PR-3b-1: wamid (envelope 'data') captured into ProviderMessageId on Submitted ──

    [Fact]
    public async Task Submitted_captures_wamid_from_string_data_into_ProviderMessageId()
    {
        var (client, _, _) = Build((_, _) => Envelope(true, "200", "req-123", "ok", data: "wamid.HBgMOTA1XXXX"));

        var result = await client.SendPlainTextAsync(Req());

        result.Outcome.Should().Be(WapCrmSendOutcome.Submitted);
        result.ProviderMessageId.Should().Be("wamid.HBgMOTA1XXXX");
        // requestID stays the (audit-only) GUID — it is NOT the correlation key (INMA C1, 2026-06-08).
        result.ProviderRequestId.Should().Be("req-123");
    }

    [Fact]
    public async Task Submitted_yields_null_ProviderMessageId_when_data_absent_empty_or_non_string()
    {
        // data = null (omitted)
        var (c1, _, _) = Build((_, _) => Envelope(true, "200", "r"));
        (await c1.SendPlainTextAsync(Req())).ProviderMessageId.Should().BeNull();

        // data = empty string
        var (c2, _, _) = Build((_, _) => Envelope(true, "200", "r", data: ""));
        (await c2.SendPlainTextAsync(Req())).ProviderMessageId.Should().BeNull();

        // data = non-string JSON (object) — defensive: never throws, just yields null
        var (c3, _, _) = Build((_, _) => Envelope(true, "200", "r", data: new { id = "x" }));
        (await c3.SendPlainTextAsync(Req())).ProviderMessageId.Should().BeNull();
    }

    // ── AC3: ProviderFailed (no retry) ────────────────────────────────

    [Fact]
    public async Task ProviderFailed_on_http200_status_false()
    {
        var (client, _, handler) = Build((_, _) => Envelope(false, "622", "req-9", "missing field"));

        var result = await client.SendPlainTextAsync(Req());

        result.Outcome.Should().Be(WapCrmSendOutcome.ProviderFailed);
        result.ProviderStatusCode.Should().Be("622");
        result.ProviderErrorMessage.Should().Be("missing field");
        result.ProviderStatus.Should().BeFalse();
        result.AttemptCount.Should().Be(1);
        handler.Captured.Should().HaveCount(1); // never retried
    }

    // ── AC3: status==true is authoritative — never retried even on a 301 code ──

    [Fact]
    public async Task StatusTrue_wins_over_envelope_rateLimit_code()
    {
        var (client, _, handler) = Build((_, _) => Envelope(true, "301", "req-5"));

        var result = await client.SendPlainTextAsync(Req());

        result.Outcome.Should().Be(WapCrmSendOutcome.Submitted);
        handler.Captured.Should().HaveCount(1); // accepted message must not be retried
    }

    // ── AC4: RateLimited via HTTP 301, retries exhausted ──────────────

    [Fact]
    public async Task RateLimited_on_http301_after_retries_exhausted_honors_RetryAfter()
    {
        var opts = new WapCrmSendOptions { MaxRateLimitRetries = 2, BaseBackoffMs = 1, MaxJitterMs = 0, TimeoutMs = 5_000 };
        var (client, _, handler) = Build((_, _) => Redirect(301, retryAfterSeconds: 2), opts);

        var result = await client.SendPlainTextAsync(Req());

        result.Outcome.Should().Be(WapCrmSendOutcome.RateLimited);
        result.RetryAfter.Should().Be(TimeSpan.FromSeconds(2));
        result.AttemptCount.Should().Be(3); // 1 + 2 retries
        handler.Captured.Should().HaveCount(3);
    }

    // ── AC4: RateLimited via envelope statusCode 301 (no Retry-After header) ──

    [Fact]
    public async Task RateLimited_on_envelope_301_after_retries_exhausted()
    {
        var opts = new WapCrmSendOptions { MaxRateLimitRetries = 1, BaseBackoffMs = 1, MaxJitterMs = 0, TimeoutMs = 5_000 };
        var (client, _, handler) = Build((_, _) => Envelope(false, "301"), opts);

        var result = await client.SendPlainTextAsync(Req());

        result.Outcome.Should().Be(WapCrmSendOutcome.RateLimited);
        result.ProviderStatusCode.Should().Be("301");
        result.RetryAfter.Should().BeNull();
        result.AttemptCount.Should().Be(2);
    }

    // ── AC4: recovery — 302 then 200 ──────────────────────────────────

    [Fact]
    public async Task Recovers_when_302_is_followed_by_a_200()
    {
        var (client, _, handler) = Build((_, i) => i == 0 ? Redirect(302) : Envelope(true, "200", "req-ok"));

        var result = await client.SendPlainTextAsync(Req());

        result.Outcome.Should().Be(WapCrmSendOutcome.Submitted);
        result.ProviderRequestId.Should().Be("req-ok");
        result.AttemptCount.Should().Be(2);
        handler.Captured.Should().HaveCount(2);
    }

    // ── AC5: timeout => Ambiguous, no retry ───────────────────────────

    [Fact]
    public async Task Ambiguous_on_timeout_and_not_retried()
    {
        var (client, _, handler) = Build((_, _) => throw new TaskCanceledException());

        var result = await client.SendPlainTextAsync(Req());

        result.Outcome.Should().Be(WapCrmSendOutcome.Ambiguous);
        result.HttpStatusCode.Should().Be(0);
        result.AttemptCount.Should().Be(1);
        handler.Captured.Should().HaveCount(1); // never retried (duplicate-send risk)
    }

    // ── AC5: caller cancellation propagates (not classified) ──────────

    [Fact]
    public async Task Caller_cancellation_is_rethrown()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (client, _, _) = Build((_, _) => Envelope(true, "200"));

        var act = () => client.SendPlainTextAsync(Req(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── AC5: transport / parse failures => TransportError ─────────────

    [Fact]
    public async Task TransportError_on_HttpRequestException()
    {
        var (client, _, handler) = Build((_, _) => throw new HttpRequestException("connection refused"));

        var result = await client.SendPlainTextAsync(Req());

        result.Outcome.Should().Be(WapCrmSendOutcome.TransportError);
        result.HttpStatusCode.Should().Be(0);
        result.AttemptCount.Should().Be(1);
        handler.Captured.Should().HaveCount(1); // not retried
    }

    [Fact]
    public async Task TransportError_on_unparseable_body()
    {
        var (client, _, _) = Build((_, _) => Json(HttpStatusCode.OK, "<<not json>>"));

        var result = await client.SendPlainTextAsync(Req());

        result.Outcome.Should().Be(WapCrmSendOutcome.TransportError);
        result.HttpStatusCode.Should().Be(200);
    }

    // ── AC2: per-request secret isolation (sequential + concurrent) ───

    [Fact]
    public async Task Secret_is_per_request_sequential_never_on_default_headers()
    {
        var (client, http, handler) = Build((_, _) => Envelope(true, "200", "r"));

        await client.SendPlainTextAsync(Req("secret-A"));
        await client.SendPlainTextAsync(Req("secret-B"));

        handler.Captured.Select(c => c.Secret).Should().Equal("secret-A", "secret-B");
        http.DefaultRequestHeaders.Contains("X-CIB-SecretKey").Should().BeFalse();
    }

    [Fact]
    public async Task Secret_does_not_leak_across_concurrent_sends_on_a_pooled_client()
    {
        var (client, http, handler) = Build((_, _) => Envelope(true, "200", "r"));
        var secrets = Enumerable.Range(0, 20).Select(i => $"secret-{i}").ToArray();

        await Task.WhenAll(secrets.Select(s => client.SendPlainTextAsync(Req(s))));

        // Every captured secret is exactly one of the inputs; the full multiset matches
        // (no loss, no duplication, no cross-contamination across the shared HttpClient).
        handler.Captured.Select(c => c.Secret).Should().BeEquivalentTo(secrets);
        http.DefaultRequestHeaders.Contains("X-CIB-SecretKey").Should().BeFalse();
    }

    // ── Caller-contract validation (fail-fast, secret never sent) ─────

    [Fact]
    public async Task Throws_on_blank_secret_without_sending()
    {
        var (client, _, handler) = Build((_, _) => Envelope(true, "200"));

        var act = () => client.SendPlainTextAsync(Req(secret: "   "));

        await act.Should().ThrowAsync<ArgumentException>();
        handler.Captured.Should().BeEmpty();
    }
}
