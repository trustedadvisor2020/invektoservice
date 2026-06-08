using System.Text.Json;
using FluentAssertions;
using Invekto.Shared.DTOs.Outbound;

namespace InvektoServis.Tests.Shared.Outbound;

/// <summary>
/// PR-3b-2: the one piece of real logic in the Backend ack branch is the WapCRM ack Status int -> our
/// delivery-status string map (+ failed_reason truncation). Unit-covered here so the handler stays thin
/// and the mapping is verified without a Backend integration host.
/// </summary>
public class WapCrmAckMappingTests
{
    [Theory]
    [InlineData(1, "sent")]
    [InlineData(2, "delivered")]
    [InlineData(3, "read")]    // WapCRM "Viewed"
    [InlineData(4, "failed")]  // WapCRM "NotSent"
    public void MapStatus_KnownAckStatus_MapsToDeliveryString(int ackStatus, string expected)
    {
        WapCrmAckMapping.MapStatus(ackStatus).Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]   // Pending
    [InlineData(5)]   // Deleted
    [InlineData(-1)]
    [InlineData(99)]
    public void MapStatus_UnsupportedStatus_ReturnsNull(int ackStatus)
    {
        WapCrmAckMapping.MapStatus(ackStatus).Should().BeNull();
    }

    [Fact]
    public void TruncateFailedReason_NullOrEmpty_ReturnedAsIs()
    {
        WapCrmAckMapping.TruncateFailedReason(null).Should().BeNull();
        WapCrmAckMapping.TruncateFailedReason("").Should().Be("");
    }

    [Fact]
    public void TruncateFailedReason_ShortReason_Unchanged()
    {
        const string reason = "Message Undeliverable (131026)";
        WapCrmAckMapping.TruncateFailedReason(reason).Should().Be(reason);
    }

    [Fact]
    public void TruncateFailedReason_OverCap_TruncatedTo500()
    {
        var longReason = new string('x', 600);

        var result = WapCrmAckMapping.TruncateFailedReason(longReason);

        result.Should().HaveLength(WapCrmAckMapping.FailedReasonMaxLength);
        result.Should().HaveLength(500);
    }

    private static JsonElement Parse(string json)
    {
        // Clone() detaches the element from the document so it stays valid after the JsonDocument
        // (IDisposable) is disposed — no leaked document.
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Theory]
    [InlineData("{\"InstanceID\":505024,\"InstanceMessageID\":\"wamid.X\",\"Status\":3,\"StatusText\":\"Viewed\"}")] // full ack
    [InlineData("{\"Status\":1}")]                          // Status only
    [InlineData("{\"InstanceMessageID\":\"wamid.X\"}")]     // wamid only
    public void IsDeliveryAckShape_AckPayload_ReturnsTrue(string json)
    {
        WapCrmAckMapping.IsDeliveryAckShape(Parse(json)).Should().BeTrue();
    }

    [Theory]
    [InlineData("{\"InstanceID\":\"505024\",\"messages\":[{\"id\":\"wamid.X\",\"body\":\"hi\"}]}")] // inbound message
    [InlineData("{\"messages\":[]}")]                                    // empty messages (not an ack)
    [InlineData("{\"messages\":[],\"Status\":1,\"InstanceMessageID\":\"wamid.X\"}")] // AMBIGUOUS: has messages AND ack fields -> NOT an ack
    [InlineData("{\"foo\":1}")]                                          // neither shape
    [InlineData("{}")]                                                    // empty object
    public void IsDeliveryAckShape_NonAckOrAmbiguousPayload_ReturnsFalse(string json)
    {
        WapCrmAckMapping.IsDeliveryAckShape(Parse(json)).Should().BeFalse();
    }

    [Fact]
    public void IsDeliveryAckShape_NonObjectRoot_ReturnsFalse()
    {
        WapCrmAckMapping.IsDeliveryAckShape(Parse("[1,2,3]")).Should().BeFalse();
    }
}
