using System.Diagnostics;
using System.Text;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;

namespace Invekto.AgentAI.Middleware;

public sealed class TrafficLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JsonLinesLogger _logger;

    private static readonly string[] SkipPaths = { "/health", "/ready" };

    public TrafficLoggingMiddleware(RequestDelegate next, JsonLinesLogger logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (SkipPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();

        string? requestBody = null;
        if (context.Request.ContentLength > 0 && context.Request.ContentLength < 50000)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(
                context.Request.Body, Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            string? responseBody = null;
            if (responseBodyStream.Length > 0 && responseBodyStream.Length < 50000)
            {
                responseBodyStream.Position = 0;
                responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();
            }

            responseBodyStream.Position = 0;
            await responseBodyStream.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;

            var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault()
                ?? context.TraceIdentifier;
            var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "-";
            var chatId = context.Request.Headers["X-Chat-Id"].FirstOrDefault() ?? "-";

            var logContext = new RequestContext
            {
                RequestId = requestId,
                TenantId = tenantId,
                ChatId = chatId
            };

            var level = context.Response.StatusCode >= 400 ? "WARN" : "INFO";
            var message = $"{context.Request.Method} {path} -> {context.Response.StatusCode}";

            _logger.LogTraffic(level, message, logContext, path,
                context.Request.Method, sw.ElapsedMilliseconds,
                context.Response.StatusCode, requestBody, responseBody);
        }
    }
}

public static class TrafficLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseTrafficLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TrafficLoggingMiddleware>();
    }
}
