using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Invekto.Shared.Constants;

namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// External HTTP API call node. Auto-chain.
/// Production: makes real HTTP call with SSRF protection + timeout.
/// Simulation: returns static mock response (no real call).
/// 2 output handles: success, error.
/// Variables set: {response_variable} (response body JSON), api_status_code, api_error_message.
/// </summary>
public sealed class ApiCallHandler : INodeHandler
{
    private readonly IHttpClientFactory _httpClientFactory;

    private const int MaxResponseBytes = 64 * 1024; // 64KB max response body

    public string NodeType => "action_api_call";

    public ApiCallHandler(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var label = node.GetData("label", node.Id);
        var method = node.GetData("method", "GET").ToUpperInvariant();
        var rawUrl = node.GetData("url", "");
        var bodyTemplate = node.GetData("body_template", "");
        var responseVariable = node.GetData("response_variable", "api_response");
        var timeoutMs = ParseInt(node.GetData("timeout_ms"), 5000);

        // Substitute variables in URL and body
        var url = ctx.Evaluator.Substitute(rawUrl, ctx.State.Variables);
        var body = ctx.Evaluator.Substitute(bodyTemplate, ctx.State.Variables);

        // Simulation: return static mock response
        if (ctx.IsSimulation)
        {
            ctx.Logger.StepInfo(
                $"ApiCall '{label}': simulation mock response (no real HTTP call). {method} {url}",
                ctx.RequestId);

            var mockJson = JsonSerializer.Serialize(new { status = 200, mock = true, message = "Simulated API response" });
            return new NodeResult
            {
                MessageText = null,
                Action = NodeAction.Continue,
                OutputHandle = "success",
                VariableUpdates = new Dictionary<string, string>
                {
                    [responseVariable] = mockJson,
                    ["api_status_code"] = "200",
                    ["api_error_message"] = ""
                }
            };
        }

        // Production: SSRF validation
        var ssrfError = await ValidateUrlSsrf(url, ctx);
        if (ssrfError != null)
        {
            ctx.Logger.SystemWarn($"ApiCall '{label}': [{ErrorCodes.AutomationApiCallSsrfBlocked}] SSRF blocked -- {ssrfError}");
            return new NodeResult
            {
                MessageText = null,
                Action = NodeAction.Continue,
                OutputHandle = "error",
                VariableUpdates = new Dictionary<string, string>
                {
                    [responseVariable] = "",
                    ["api_status_code"] = "0",
                    ["api_error_message"] = $"[{ErrorCodes.AutomationApiCallSsrfBlocked}] {ssrfError}"
                }
            };
        }

        // HTTP warning for non-HTTPS
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Logger.SystemWarn($"ApiCall '{label}': HTTP (not HTTPS) request to {url} -- consider using HTTPS");
        }

        // Make the HTTP call
        try
        {
            using var httpClient = _httpClientFactory.CreateClient("ApiCallHandler");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Math.Clamp(timeoutMs, 100, 30000));

            using var request = new HttpRequestMessage(ParseMethod(method), url);

            // Set headers from node config
            var headersJson = node.GetData("headers");
            if (!string.IsNullOrEmpty(headersJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(headersJson);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        var headerValue = ctx.Evaluator.Substitute(prop.Value.GetString() ?? "", ctx.State.Variables);
                        request.Headers.TryAddWithoutValidation(prop.Name, headerValue);
                    }
                }
                catch (JsonException ex)
                {
                    ctx.Logger.SystemWarn($"ApiCall '{label}': headers JSON parse failed: {ex.Message}");
                }
            }

            // Set body for methods that support it
            if (method is "POST" or "PUT" && !string.IsNullOrEmpty(body))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            var responseBody = await ReadBoundedResponseAsync(response, cts.Token);
            var statusCode = (int)response.StatusCode;

            ctx.Logger.StepInfo(
                $"ApiCall '{label}': {method} {url} -> HTTP {statusCode} ({responseBody.Length} bytes)",
                ctx.RequestId);

            if (response.IsSuccessStatusCode)
            {
                return new NodeResult
                {
                    MessageText = null,
                    Action = NodeAction.Continue,
                    OutputHandle = "success",
                    VariableUpdates = new Dictionary<string, string>
                    {
                        [responseVariable] = responseBody,
                        ["api_status_code"] = statusCode.ToString(),
                        ["api_error_message"] = ""
                    }
                };
            }

            // HTTP error (4xx, 5xx) -- route to error handle
            return new NodeResult
            {
                MessageText = null,
                Action = NodeAction.Continue,
                OutputHandle = "error",
                VariableUpdates = new Dictionary<string, string>
                {
                    [responseVariable] = responseBody,
                    ["api_status_code"] = statusCode.ToString(),
                    ["api_error_message"] = $"[{ErrorCodes.AutomationApiCallHttpError}] HTTP {statusCode}: {response.ReasonPhrase}"
                }
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // App shutting down
        }
        catch (OperationCanceledException)
        {
            ctx.Logger.SystemWarn($"ApiCall '{label}': [{ErrorCodes.AutomationApiCallTimeout}] timeout after {timeoutMs}ms -- {method} {url}");
            return new NodeResult
            {
                MessageText = null,
                Action = NodeAction.Continue,
                OutputHandle = "error",
                VariableUpdates = new Dictionary<string, string>
                {
                    [responseVariable] = "",
                    ["api_status_code"] = "0",
                    ["api_error_message"] = $"[{ErrorCodes.AutomationApiCallTimeout}] Zaman asimi ({timeoutMs}ms)"
                }
            };
        }
        catch (HttpRequestException ex)
        {
            ctx.Logger.SystemWarn($"ApiCall '{label}': HTTP request failed -- {ex.Message}");
            return new NodeResult
            {
                MessageText = null,
                Action = NodeAction.Continue,
                OutputHandle = "error",
                VariableUpdates = new Dictionary<string, string>
                {
                    [responseVariable] = "",
                    ["api_status_code"] = "0",
                    ["api_error_message"] = $"Baglanti hatasi: {ex.Message}"
                }
            };
        }
    }

    /// <summary>
    /// Read response body with size limit to prevent memory exhaustion.
    /// Truncates at MaxResponseBytes.
    /// </summary>
    private static async Task<string> ReadBoundedResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[MaxResponseBytes];
        var totalRead = 0;

        while (totalRead < MaxResponseBytes)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, MaxResponseBytes - totalRead), ct);
            if (bytesRead == 0) break;
            totalRead += bytesRead;
        }

        return Encoding.UTF8.GetString(buffer, 0, totalRead);
    }

    /// <summary>
    /// Validate URL for SSRF protection. Returns error message if blocked, null if safe.
    /// Blocks: localhost, 127.x, 10.x, 172.16-31.x, 192.168.x, 169.254.x, ::1, fc00::/7
    /// </summary>
    private static async Task<string?> ValidateUrlSsrf(string url, ExecutionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "URL bos";

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return $"Gecersiz URL: {url}";

        if (uri.Scheme != "http" && uri.Scheme != "https")
            return $"Desteklenmeyen protokol: {uri.Scheme} (sadece http/https)";

        var host = uri.Host;

        // Direct localhost check
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.Ordinal) ||
            host.Equals("::1", StringComparison.Ordinal) ||
            host.Equals("[::1]", StringComparison.Ordinal) ||
            host.Equals("0.0.0.0", StringComparison.Ordinal))
        {
            return $"Dahili adres engellendi: {host}";
        }

        // DNS resolve to check actual IP
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host);
            foreach (var ip in addresses)
            {
                if (IsPrivateOrReservedIp(ip))
                    return $"Dahili IP adresi engellendi: {ip} (host: {host})";
            }
        }
        catch (SocketException ex)
        {
            return $"DNS cozumlemesi basarisiz: {host} ({ex.Message})";
        }

        return null; // Safe
    }

    /// <summary>
    /// Check if IP address is private, loopback, or reserved.
    /// </summary>
    private static bool IsPrivateOrReservedIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // IPv6 private: fc00::/7 (unique local), fe80::/10 (link-local)
            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC) return true; // fc00::/7
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80) return true; // fe80::/10
            return false;
        }

        // IPv4
        var ipBytes = ip.GetAddressBytes();
        if (ipBytes.Length != 4) return false;

        // 10.0.0.0/8
        if (ipBytes[0] == 10) return true;
        // 172.16.0.0/12
        if (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31) return true;
        // 192.168.0.0/16
        if (ipBytes[0] == 192 && ipBytes[1] == 168) return true;
        // 169.254.0.0/16 (link-local)
        if (ipBytes[0] == 169 && ipBytes[1] == 254) return true;
        // 127.0.0.0/8 (redundant with IsLoopback, but explicit)
        if (ipBytes[0] == 127) return true;

        return false;
    }

    private static HttpMethod ParseMethod(string method) => method switch
    {
        "POST" => HttpMethod.Post,
        "PUT" => HttpMethod.Put,
        "DELETE" => HttpMethod.Delete,
        _ => HttpMethod.Get
    };

    private static int ParseInt(string? raw, int fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;
        return int.TryParse(raw, out var v) ? v : fallback;
    }
}
