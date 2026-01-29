namespace Invekto.Contracts.Constants;

/// <summary>
/// Standard error codes for all Invekto services.
/// Format: SVC-CAT-NNN (Service-Category-Number)
/// </summary>
public static class ErrorCodes
{
    #region INV - Invekto Core

    // INV-VAL - Validation
    public const string INV_VAL_001 = "INV-VAL-001"; // Invalid request format
    public const string INV_VAL_002 = "INV-VAL-002"; // Missing required field
    public const string INV_VAL_003 = "INV-VAL-003"; // Invalid field value
    public const string INV_VAL_004 = "INV-VAL-004"; // Invalid idempotency key

    // INV-AUTH - Authentication
    public const string INV_AUTH_001 = "INV-AUTH-001"; // Unauthorized
    public const string INV_AUTH_002 = "INV-AUTH-002"; // Forbidden
    public const string INV_AUTH_003 = "INV-AUTH-003"; // Token expired

    // INV-NET - Network
    public const string INV_NET_001 = "INV-NET-001"; // Connection timeout
    public const string INV_NET_002 = "INV-NET-002"; // Service unavailable
    public const string INV_NET_003 = "INV-NET-003"; // Circuit breaker open

    // INV-SYS - System
    public const string INV_SYS_001 = "INV-SYS-001"; // Internal server error
    public const string INV_SYS_002 = "INV-SYS-002"; // Redis unavailable
    public const string INV_SYS_003 = "INV-SYS-003"; // RabbitMQ unavailable
    public const string INV_SYS_004 = "INV-SYS-004"; // Rate limit exceeded
    public const string INV_SYS_005 = "INV-SYS-005"; // Service degraded

    #endregion

    #region CHA - Chat Analysis

    // CHA-VAL - Validation
    public const string CHA_VAL_001 = "CHA-VAL-001"; // Invalid chat format
    public const string CHA_VAL_002 = "CHA-VAL-002"; // Chat too large
    public const string CHA_VAL_003 = "CHA-VAL-003"; // Invalid tenant ID

    // CHA-BUS - Business
    public const string CHA_BUS_001 = "CHA-BUS-001"; // Analysis failed
    public const string CHA_BUS_002 = "CHA-BUS-002"; // Insufficient data
    public const string CHA_BUS_003 = "CHA-BUS-003"; // Analysis in progress

    // CHA-EXT - External
    public const string CHA_EXT_001 = "CHA-EXT-001"; // Backend unavailable
    public const string CHA_EXT_002 = "CHA-EXT-002"; // Backend timeout
    public const string CHA_EXT_003 = "CHA-EXT-003"; // Backend error

    #endregion

    #region MSG - Messaging

    // MSG-NET - Network
    public const string MSG_NET_001 = "MSG-NET-001"; // Queue unavailable
    public const string MSG_NET_002 = "MSG-NET-002"; // Publish failed
    public const string MSG_NET_003 = "MSG-NET-003"; // Consumer error

    // MSG-BUS - Business
    public const string MSG_BUS_001 = "MSG-BUS-001"; // Message rejected
    public const string MSG_BUS_002 = "MSG-BUS-002"; // Duplicate message
    public const string MSG_BUS_003 = "MSG-BUS-003"; // Max retries exceeded

    #endregion

    #region ADM - Admin

    // ADM-AUTH - Authentication
    public const string ADM_AUTH_001 = "ADM-AUTH-001"; // Login required
    public const string ADM_AUTH_002 = "ADM-AUTH-002"; // Invalid email
    public const string ADM_AUTH_003 = "ADM-AUTH-003"; // OAuth failed

    // ADM-BUS - Business
    public const string ADM_BUS_001 = "ADM-BUS-001"; // DLQ message not found
    public const string ADM_BUS_002 = "ADM-BUS-002"; // Reprocess failed
    public const string ADM_BUS_003 = "ADM-BUS-003"; // Flag update failed

    #endregion

    #region GW - Gateway

    // GW-NET - Network
    public const string GW_NET_001 = "GW-NET-001"; // Upstream timeout
    public const string GW_NET_002 = "GW-NET-002"; // No healthy upstream
    public const string GW_NET_003 = "GW-NET-003"; // Request too large

    // GW-SYS - System
    public const string GW_SYS_001 = "GW-SYS-001"; // Rate limit exceeded
    public const string GW_SYS_002 = "GW-SYS-002"; // Connection limit

    #endregion
}
