namespace Chatinbox.Shared.Contracts.Voice;

/// <summary>
/// FEAT-VFB F0.5 Chunk B (AD-22 + AD-25): tenant-aware Voice Test runtime context.
///
/// Built by VoiceRuntime AFTER the F0.5 impersonation handshake passes (Chunk A) and
/// BEFORE the Realtime session.update is sent. Carries the authoritative server-side
/// view of the impersonation target — used by InstructionsBuilder to render the Turkce
/// system prompt and by ToolExecutor to mint per-tenant Knowledge JWTs.
///
/// AD-25 invariant: this context is rebuilt server-side via TenantInfoClient + FlowInfoClient
/// using a fresh service JWT. The dropdown values the browser sends (?tenant_id, ?flow_id) are
/// only used to identify the target — names/sectors are NEVER trusted from the browser
/// (defense-in-depth: spoofed dropdown values cannot influence the model prompt).
///
/// F0 backward-compat: legacy microphone mode does NOT build this context (descriptor.TenantId=0
/// + appsettings default instructions). Only F0.5 path produces a VoiceTestContext.
/// </summary>
public sealed record VoiceTestContext(
    int TargetTenantId,            // Impersonated tenant (positive int, validated in Chunk A handshake)
    string TenantName,             // Backend /api/ops/tenants → tenant_name (fallback "bu firma" if blank)
    string Sector,                 // Backend /api/ops/tenants → sector (fallback "" if null — instructions builder picks alt template)
    int FlowId,                    // Selected flow_id (positive int, validated in Chunk A handshake)
    string FlowName,               // Automation /api/v1/flows/{tid}/{fid} → flow_name (fallback "varsayilan akis" if blank)
    int CallerTenantId,            // Caller JWT tenant_id — sysadmin (0) under F0.5 impersonation gate (INV-VR-020)
    string ImpersonationMode       // "voice_test_f05" (Chunk A descriptor.ProviderMetadata aligned)
);
