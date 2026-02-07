using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.OrganizationService.Helper;

public static class RsaKeyLoader
{
    public static RsaSecurityKey LoadPrivateKey(string path)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(path));
        return new RsaSecurityKey(rsa);
    }

    public static RsaSecurityKey LoadPublicKey(string path)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(path));
        return new RsaSecurityKey(rsa);
    }
}
