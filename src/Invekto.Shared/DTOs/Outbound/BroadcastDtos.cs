using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Outbound;

/// <summary>
/// POST /api/v1/broadcast/send request body.
/// Main App sends recipients + template_id to start a broadcast.
/// </summary>
public sealed class BroadcastSendRequest
{
    [JsonPropertyName("template_id")]
    public int TemplateId { get; set; }

    [JsonPropertyName("recipients")]
    public List<BroadcastRecipient> Recipients { get; set; } = new();

    [JsonPropertyName("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }
}

public sealed class BroadcastRecipient
{
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = "";

    [JsonPropertyName("variables")]
    public Dictionary<string, string>? Variables { get; set; }
}

/// <summary>
/// 202 Accepted response for broadcast send.
/// </summary>
public sealed class BroadcastSendResponse
{
    [JsonPropertyName("broadcast_id")]
    public Guid BroadcastId { get; set; }

    [JsonPropertyName("total_recipients")]
    public int TotalRecipients { get; set; }

    [JsonPropertyName("queued")]
    public int Queued { get; set; }

    [JsonPropertyName("skipped_optout")]
    public int SkippedOptout { get; set; }
}

/// <summary>
/// GET /api/v1/broadcast/{broadcastId}/status response.
/// </summary>
public sealed class BroadcastStatusResponse
{
    [JsonPropertyName("broadcast_id")]
    public Guid BroadcastId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("total_recipients")]
    public int TotalRecipients { get; set; }

    [JsonPropertyName("queued")]
    public int Queued { get; set; }

    [JsonPropertyName("sent")]
    public int Sent { get; set; }

    [JsonPropertyName("delivered")]
    public int Delivered { get; set; }

    [JsonPropertyName("read")]
    public int Read { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }
}
