using System.Text.Json;

namespace Chatinbox.VoiceRuntime.Tools;

/// <summary>
/// FEAT-VFB F0.5 Chunk C: contract for a Realtime function tool. Each tool ships its OpenAI
/// session.update.tools[] metadata (name, description, JSON Schema parameters) plus an execute
/// method invoked when the model emits response.function_call_arguments.done with a matching name.
///
/// Name MUST be snake_case (OpenAI validation). Description is English (AD-23: model alignment).
/// </summary>
public interface IVoiceTool
{
    /// <summary>OpenAI tool name (snake_case, must be unique per session).</summary>
    string Name { get; }

    /// <summary>English description shown to the model (AD-23).</summary>
    string Description { get; }

    /// <summary>JSON Schema for arguments (object with required/properties).</summary>
    JsonElement Parameters { get; }

    /// <summary>
    /// Execute the tool with the model-provided arguments JSON. Caller injects the impersonation
    /// target tenant_id — never trust the model to pass a tenant. Should respect ct (session
    /// cancellation + per-call timeout). Never throws on tool-domain failures — those are returned
    /// as ToolExecutionResult.Status="error" with the structured error JSON in OutputJson.
    /// </summary>
    Task<ToolExecutionResult> ExecuteAsync(int tenantId, string argumentsJson, CancellationToken ct);
}

/// <summary>
/// Result of a single tool execution. OutputJson is the exact string forwarded back to the model
/// via conversation.item.create function_call_output — caller does not re-serialize.
/// ResultCount + Status feed the tool_call_completed HUD frame (AD-29).
/// </summary>
public sealed record ToolExecutionResult(
    string OutputJson,
    int ResultCount,
    string Status,        // "ok" | "error"
    string? ErrorCode     // INV-VR-xxx when Status="error"; null when ok
);
