using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Chatinbox.Tests._Shared.Infrastructure;

/// <summary>
/// JWT token generator for test authentication.
/// Uses the same signing algorithm as production JwtValidator.
/// </summary>
public static class TestJwtTokenHelper
{
    public const string TestSecretKey = "InvektoTestSecretKey2026ForUnitTests!!";
    public const int DefaultTenantId = 1001;
    public const int DefaultUserId = 42;
    public const string DefaultRole = "admin";

    public static string GenerateToken(
        int tenantId = DefaultTenantId,
        int userId = DefaultUserId,
        string role = DefaultRole,
        TimeSpan? expiry = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("user_id", userId.ToString()),
            new Claim("role", role),
            new Claim("source", "test")
        };

        var token = new JwtSecurityToken(
            issuer: null,
            audience: null,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiry ?? TimeSpan.FromHours(1)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void AddAuthHeader(HttpClient client, int tenantId = DefaultTenantId, int userId = DefaultUserId)
    {
        var token = GenerateToken(tenantId, userId);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public static void AddOpsAuthHeader(HttpClient client)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:admin123"));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
    }
}
