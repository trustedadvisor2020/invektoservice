using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Inma.Dtos;

/// <summary>
/// Navigation metadata served to the INMA (WapCRM) parent shell so the outer
/// sidebar can render Turkish labels that match Invekto feature surface.
/// Tenant-only; ops items are excluded at the source.
/// Icon names are lucide-react identifiers (kebab-case); the consumer maps
/// them to its own icon library.
/// </summary>
public sealed class InmaNavResponse
{
    [JsonPropertyName("sections")]
    public IReadOnlyList<InmaNavSection> Sections { get; init; } = [];
}

public sealed class InmaNavSection
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("items")]
    public IReadOnlyList<InmaNavItem> Items { get; init; } = [];
}

public sealed class InmaNavItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; init; } = string.Empty;
}
