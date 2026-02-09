using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Invekto.Shared.Auth;

/// <summary>
/// Validates JWT tokens from Main App using shared HMAC-SHA256 key.
/// GR-1.9: Thread-safe, stateless validator. Register as singleton.
/// </summary>
public sealed class JwtValidator
{
    private readonly TokenValidationParameters _validationParameters;

    public JwtValidator(JwtSettings settings)
    {
        var keyBytes = Encoding.UTF8.GetBytes(settings.SecretKey);
        if (keyBytes.Length < 32)
            throw new ArgumentException("JWT SecretKey must be at least 32 bytes (256 bits). Current key is " + keyBytes.Length + " bytes.");

        var securityKey = new SymmetricSecurityKey(keyBytes);

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ValidateIssuer = !string.IsNullOrEmpty(settings.Issuer),
            ValidIssuer = settings.Issuer,
            ValidateAudience = !string.IsNullOrEmpty(settings.Audience),
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(settings.ClockSkewSeconds)
        };
    }

    /// <summary>
    /// Validate a JWT token and extract TenantContext.
    /// Returns (TenantContext, null) on success, (null, errorMessage) on failure.
    /// </summary>
    public (TenantContext? Context, string? Error) ValidateToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, _validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken)
                return (null, "Token is not a valid JWT");

            // Extract claims
            var tenantIdClaim = principal.FindFirst("tenant_id")?.Value;
            var userIdClaim = principal.FindFirst("user_id")?.Value
                              ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? principal.FindFirst("sub")?.Value;
            var roleClaim = principal.FindFirst("role")?.Value
                            ?? principal.FindFirst(ClaimTypes.Role)?.Value
                            ?? "agent"; // default role

            if (string.IsNullOrEmpty(tenantIdClaim) || !int.TryParse(tenantIdClaim, out var tenantId))
                return (null, "Missing or invalid 'tenant_id' claim (expected integer)");

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return (null, "Missing or invalid 'user_id' or 'sub' claim (expected integer)");

            var context = new TenantContext
            {
                TenantId = tenantId,
                UserId = userId,
                Role = roleClaim
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
}
