using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Invekto.Shared.Auth;

/// <summary>
/// Generates short-lived JWT tokens for service-to-service communication.
/// GR-2.1 Phase B: Backend (Basic Auth) -> Knowledge (JWT) bridge.
/// Thread-safe, stateless. Register as singleton alongside JwtValidator.
/// </summary>
public sealed class JwtGenerator
{
    private readonly SigningCredentials _signingCredentials;
    private readonly string? _issuer;
    private readonly string? _audience;

    public JwtGenerator(JwtSettings settings)
    {
        var keyBytes = Encoding.UTF8.GetBytes(settings.SecretKey);
        if (keyBytes.Length < 32)
            throw new ArgumentException(
                $"JWT SecretKey must be at least 32 bytes (256 bits). Current key is {keyBytes.Length} bytes.");

        var securityKey = new SymmetricSecurityKey(keyBytes);
        _signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        _issuer = settings.Issuer;
        _audience = settings.Audience;
    }

    /// <summary>
    /// Generate a short-lived service JWT for internal calls.
    /// Claims: tenant_id, user_id=0, role=service, source=backend_proxy.
    /// </summary>
    public string GenerateServiceToken(int tenantId, TimeSpan? expiry = null)
    {
        return GenerateToken(tenantId, "service", "backend_proxy", expiry ?? TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Generate a JWT with custom role and source claims.
    /// Used by FlowBuilder login, service-to-service, and other token needs.
    /// </summary>
    public string GenerateToken(int tenantId, string role, string source, TimeSpan expiry, string userId = "0")
    {
        var expires = DateTime.UtcNow.Add(expiry);

        var claims = new[]
        {
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("user_id", userId),
            new Claim("role", role),
            new Claim("source", source)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expires,
            signingCredentials: _signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
