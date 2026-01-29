namespace Invekto.Contracts.Enums;

/// <summary>
/// Error categories for classification.
/// </summary>
public enum ErrorCategory
{
    /// <summary>Input validation errors (VAL)</summary>
    Validation,

    /// <summary>Authentication/Authorization errors (AUTH)</summary>
    Authentication,

    /// <summary>Network/Connection errors (NET)</summary>
    Network,

    /// <summary>Data/Database errors (DATA)</summary>
    Data,

    /// <summary>Business logic errors (BUS)</summary>
    Business,

    /// <summary>System/Infrastructure errors (SYS)</summary>
    System,

    /// <summary>External service errors (EXT)</summary>
    External
}
