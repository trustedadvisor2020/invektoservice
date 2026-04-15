// Adim 2 Paket B + Adim 3 Paket 1/3-B1: service-local Zoho error code constants (mirror of arch/errors.md INV-INT-110..130).
namespace Invekto.Integrations.Services.Zoho;

public static class ZohoErrorCodes
{
    public const string UnknownRegion            = "INV-INT-110";
    public const string OAuthStateInvalid        = "INV-INT-111";
    public const string OAuthStateTenantMismatch = "INV-INT-112";
    public const string TokenExchangeFailed      = "INV-INT-113";
    public const string TokenRefreshFailed       = "INV-INT-114";
    public const string ConnectionNotFound       = "INV-INT-115";
    public const string RegionNotConfigured      = "INV-INT-116";
    public const string DecryptionFailed         = "INV-INT-117";
    public const string Disconnected             = "INV-INT-118";
    public const string RateLimitReached         = "INV-INT-119";

    // Adim 3 Paket 1: Sync + Blueprint + Lead + internal-auth
    public const string StageMappingNotConfigured   = "INV-INT-120";
    public const string BlueprintNotConfigured     = "INV-INT-121";
    public const string BlueprintTransitionNotFound = "INV-INT-122";
    public const string LeadNotFound                = "INV-INT-123";
    public const string InternalAuthInvalid         = "INV-INT-124";
    public const string SyncInfrastructureError     = "INV-INT-125";
    public const string InternalAuthNotConfigured   = "INV-INT-126";

    // Adim 3 Paket 2: Backend-side transport
    public const string BackendSyncTransport        = "INV-INT-127";

    // Adim 3 Paket 3-B1: Dashboard UI API surface
    public const string SyncLogNotFound             = "INV-INT-128";
    public const string SyncLogNotRetryable         = "INV-INT-129";
    public const string TokenRevokeBestEffort       = "INV-INT-130";
}
