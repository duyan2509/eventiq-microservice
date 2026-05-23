using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Eventiq.EventService.Tests;

/// <summary>
/// Verifies that custom JWT claims (orgId, orgName) are readable after token validation
/// with MapInboundClaims = false — which is the EventService production config.
/// </summary>
public class JwtClaimReadingTests
{
    private static readonly SymmetricSecurityKey _key =
        new(Encoding.UTF8.GetBytes("test-secret-key-for-unit-tests-32chars!!"));

    private string BuildToken(Dictionary<string, string> extraClaims)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "Organization"),
            new("email", "test@example.com"),
        };

        foreach (var (key, value) in extraClaims)
            claims.Add(new Claim(key, value));

        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256)
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private ClaimsPrincipal ValidateToken(string token, bool mapInboundClaims)
    {
        // Mirrors production config in Extensions.cs
        if (!mapInboundClaims)
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        var handler = new JwtSecurityTokenHandler();
        handler.MapInboundClaims = mapInboundClaims;

        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = _key,
            NameClaimType = "sub",
            RoleClaimType = ClaimTypes.Role
        }, out _);

        return principal;
    }

    [Fact]
    public void OrgName_IsReadable_WithMapInboundClaimsFalse()
    {
        var token = BuildToken(new() { ["orgId"] = "abc-123", ["orgName"] = "Acme Corp" });

        var principal = ValidateToken(token, mapInboundClaims: false);

        Assert.Equal("Acme Corp", principal.FindFirstValue("orgName"));
        Assert.Equal("abc-123", principal.FindFirstValue("orgId"));
    }

    [Fact]
    public void OrgName_IsEmpty_WhenNotInToken()
    {
        var token = BuildToken(new() { ["orgId"] = "abc-123" });

        var principal = ValidateToken(token, mapInboundClaims: false);

        var orgName = principal.FindFirstValue("orgName") ?? string.Empty;
        Assert.Equal(string.Empty, orgName);
    }

    [Fact]
    public void OrgName_IsReadable_WithMapInboundClaimsTrue()
    {
        // Custom claims (not in standard JWT map) pass through even with mapping enabled
        var token = BuildToken(new() { ["orgId"] = "abc-123", ["orgName"] = "Acme Corp" });

        var principal = ValidateToken(token, mapInboundClaims: true);

        Assert.Equal("Acme Corp", principal.FindFirstValue("orgName"));
    }
}
