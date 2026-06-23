namespace Chatinbox.Shared.Contracts.Video;

/// <summary>
/// FEAT-VCP Chunk B: envelope returned by the Integrations internal endpoint
/// <c>POST /internal/video/meetings</c> to Chatinbox.Appointments. Splits the three
/// operationally distinct outcomes Chunk A already encodes at the factory layer:
/// <list type="bullet">
/// <item><c>Skipped=true, ErrorCode="INV-INT-142"</c>, <c>Meeting=null</c> — tenant has no
/// <c>video_provider</c> configured (or selected value not yet wired). HTTP 200. Caller
/// logs and returns without persisting a meeting link. No retry — appointment stays
/// confirmed without a video surface until the tenant opts in.</item>
/// <item><c>Skipped=false, ErrorCode="INV-INT-141"</c>, <c>Meeting=null</c> — provider threw on
/// malformed input. HTTP 400. Caller logs and returns; retry is pointless because the
/// input is the problem.</item>
/// <item><c>Skipped=false, ErrorCode=null</c>, <c>Meeting=MeetingResult</c> — happy path.
/// HTTP 200. Caller persists link/provider/calendar id and schedules reminders.</item>
/// </list>
/// HTTP 503 with INV-INT-143 means the factory could not read <c>tenant_settings</c>
/// (DB outage); those responses are NOT deserialised into this envelope — they trigger
/// a Hangfire retry (INV-INT-144) in IntegrationsVideoClient instead.
/// </summary>
public sealed record VideoMeetingHopResponse(
    bool Skipped,
    string? ErrorCode,
    MeetingResult? Meeting);
