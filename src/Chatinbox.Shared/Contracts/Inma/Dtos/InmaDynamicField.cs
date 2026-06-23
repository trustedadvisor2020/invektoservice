using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Inma.Dtos;

/// <summary>
/// FEAT-DMP: INMA <c>/api/dynamicfields</c> response item.
/// <c>FieldKey</c> is the INMA-side key used inside <c>{{placeholder}}</c> and <c>DynamicMessageFields</c>.
/// <c>FieldName</c> is the tenant-authored user-facing label (e.g. cf1 → "Şehir").
/// </summary>
public sealed class InmaDynamicField
{
    [JsonPropertyName("FieldKey")]
    public string FieldKey { get; init; } = "";

    [JsonPropertyName("FieldName")]
    public string FieldName { get; init; } = "";
}
