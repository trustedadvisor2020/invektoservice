namespace Invekto.Shared.Integration;

/// <summary>
/// Configuration for async callback to Main App.
/// GR-1.9: Maps to "Integration:Callback" section in appsettings.json.
/// </summary>
public sealed class CallbackSettings
{
    /// <summary>Default callback URL for Main App (used when webhook doesn't specify callback_url)</summary>
    public required string DefaultCallbackUrl { get; init; }

    /// <summary>Max retry attempts (default: 3)</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Base delay in milliseconds for exponential backoff (default: 500ms)</summary>
    public int BaseDelayMs { get; init; } = 500;

    /// <summary>HTTP timeout per callback attempt in milliseconds (default: 5000ms)</summary>
    public int TimeoutMs { get; init; } = 5000;
}
