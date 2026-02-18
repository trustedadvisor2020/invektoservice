using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Invekto.Shared.Auth;

/// <summary>
/// Validates JWT tokens issued by inma (Invekto Main App).
/// inma uses different claim names than inse internal tokens:
///   CompanyId       → TenantId
///   nameidentifier  → UserId
///   ChatRole        → Role (1=agent, 2=admin)
///   InseFeatures    → licensed inse modules (JSON array)
/// Thread-safe, stateless. Register as singleton.
/// </summary>
public sealed class InmaJwtValidator
{
    private const string NameIdentifierClaim =
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";

    private readonly TokenValidationParameters _validationParameters;

    public InmaJwtValidator(InmaJwtSettings settings)
    {
        var keyBytes = Encoding.UTF8.GetBytes(settings.SecretKey);
        if (keyBytes.Length < 32)
            throw new ArgumentException(
                $"InmaAuth SecretKey must be at least 32 bytes. Current: {keyBytes.Length} bytes.");

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(settings.ClockSkewSeconds)
        };
    }

    /// <summary>
    /// Validate an inma JWT and extract InmaTokenContext.
    /// Returns (context, null) on success; (null, errorMessage) on failure.
    /// </summary>
    public (InmaTokenContext? Context, string? Error) ValidateToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, _validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken)
                return (null, "Token is not a valid JWT");

            // TenantId: inma 'CompanyId' claim
            var companyIdClaim = principal.FindFirst("CompanyId")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim) || !int.TryParse(companyIdClaim, out var tenantId) || tenantId <= 0)
                return (null, "Missing or invalid 'CompanyId' claim");

            // UserId: inma nameidentifier claim
            var userIdClaim = principal.FindFirst(NameIdentifierClaim)?.Value
                              ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return (null, "Missing or invalid nameidentifier claim");

            // Role: ChatRole "1"=agent, "2"=admin
            var role = MapChatRole(principal.FindFirst("ChatRole")?.Value);

            // FullName
            var fullName = principal.FindFirst("FullName")?.Value ?? string.Empty;

            // Lang
            var lang = principal.FindFirst("Lang")?.Value ?? "tr";

            // InseFeatures: malformed JSON → no licensed modules (graceful degradation: user can still log in)
            string[] inseFeatures;
            try { inseFeatures = ParseInseFeatures(principal.FindFirst("InseFeatures")?.Value); }
            catch (JsonException) { inseFeatures = []; }

            var context = new InmaTokenContext
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                FullName = fullName,
                Lang = lang,
                InseFeatures = inseFeatures
            };

            return (context, null);
        }
        catch (SecurityTokenExpiredException)
        {
            return (null, "Token expired");
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            return (null, "Invalid token signature");
        }
        catch (SecurityTokenException ex)
        {
            return (null, $"Token validation failed: {ex.Message}");
        }
    }

    private static string MapChatRole(string? chatRole) => chatRole switch
    {
        "2" => "admin",
        "1" => "agent",
        _   => "agent"  // default
    };

    private static string[] ParseInseFeatures(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return JsonSerializer.Deserialize<string[]>(raw) ?? [];
    }
}
