using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Eventiq.UserService.Helper;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.UserService.Application.Service;

public class JwtService : IJwtService
{
    private readonly RsaSecurityKey _privateKey;

    public JwtService(IConfiguration config)
    {
        var keyPath = config["Jwt:PrivateKeyPath"];
        _privateKey = RsaKeyLoader.LoadPrivateKey(keyPath);
    }

    public string GenerateAccessToken(string userId, string role, IDictionary<string, string>? extraClaims = null)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.Role, role),
        };
        if (extraClaims != null)
        {
            foreach (var keyValuePair in extraClaims)
            {
                claims.Append(new Claim(keyValuePair.Key, keyValuePair.Value));
            }
        }
        
        var creds = new SigningCredentials(
            _privateKey,
            SecurityAlgorithms.RsaSha256
        );

        var token = new JwtSecurityToken(
            issuer: "eventiq-auth",
            audience: "eventiq",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }


   


}
