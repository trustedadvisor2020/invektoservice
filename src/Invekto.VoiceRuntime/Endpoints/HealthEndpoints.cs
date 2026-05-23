using Invekto.Shared.DTOs;

namespace Invekto.VoiceRuntime.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Health check — anonymous, used by NSSM/monitoring
        app.MapGet("/health", () => Results.Ok(HealthResponse.Ok("Invekto.VoiceRuntime")));

        // Readiness — checks that dependencies (Silero model file, OpenAI key) are configured.
        // Returns 200 even if ApiKey blank (so dev runtime can start without key), but lists missing items.
        app.MapGet("/ready", (IConfiguration config) =>
        {
            var missing = new List<string>();

            var vadModelPath = config["Audio:VadModelPath"] ?? "Models/silero_vad.onnx";
            if (!File.Exists(vadModelPath))
                missing.Add($"silero_vad.onnx (expected at {vadModelPath}; see Models/README.md)");

            // AD-20: env-var-ONLY policy. Do NOT consult appsettings — plaintext key in config is FORBIDDEN.
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                missing.Add("OPENAI_API_KEY environment variable");

            return missing.Count == 0
                ? Results.Ok(new { status = "ready", service = "Invekto.VoiceRuntime" })
                : Results.Json(new { status = "degraded", service = "Invekto.VoiceRuntime", missing }, statusCode: 200);
        });
    }
}
