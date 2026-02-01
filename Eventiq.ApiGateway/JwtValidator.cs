using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.ApiGateway;


public class JwtValidator
{
    private readonly RsaSecurityKey _publicKey;

    public JwtValidator(IConfiguration config)
    {
        var keyPath = config["Jwt:PublicKeyPath"];
        _publicKey = RsaKeyLoader.LoadPublicKey(keyPath);
    }

    public ClaimsPrincipal ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        if (string.IsNullOrWhiteSpace(token))
            throw new SecurityTokenException("Token is empty");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "eventiq-auth",

            ValidateAudience = true,
            ValidAudience = "eventiq",

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _publicKey,

            RoleClaimType = ClaimTypes.Role,
            NameClaimType = JwtRegisteredClaimNames.Sub
        };

        var principal = tokenHandler.ValidateToken(
            token,
            validationParameters,
            out SecurityToken validatedToken
        );

        if (validatedToken is not JwtSecurityToken jwt ||
            jwt.Header.Alg != SecurityAlgorithms.RsaSha256)
        {
            throw new SecurityTokenException("Invalid signing algorithm");
        }

        return principal;
    }
}
public static class RsaKeyLoader
{

    public static RsaSecurityKey LoadPublicKey(string path)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(path));
        return new RsaSecurityKey(rsa);
    }
}
