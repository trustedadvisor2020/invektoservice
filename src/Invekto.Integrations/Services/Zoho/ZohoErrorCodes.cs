// Adim 2 Paket B: service-local Zoho error code constants (mirror of arch/errors.md INV-INT-110..119).
namespace Invekto.Integrations.Services.Zoho;

public static class ZohoErrorCodes
{
    public const string UnknownRegion           = "INV-INT-110";
    public const string OAuthStateInvalid       = "INV-INT-111";
    public const string OAuthStateTenantMismatch = "INV-INT-112";
    public const string TokenExchangeFailed     = "INV-INT-113";
    public const string TokenRefreshFailed      = "INV-INT-114";
    public const string ConnectionNotFound      = "INV-INT-115";
    public const string RegionNotConfigured     = "INV-INT-116";
    public const string DecryptionFailed        = "INV-INT-117";
    public const string Disconnected            = "INV-INT-118";
    public const string RateLimitReached        = "INV-INT-119";
}
