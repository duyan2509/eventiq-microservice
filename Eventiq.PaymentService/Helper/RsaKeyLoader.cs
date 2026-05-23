using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.PaymentService.Helper;

public static class RsaKeyLoader
{
    public static RsaSecurityKey LoadPublicKey(string path)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(path));
        return new RsaSecurityKey(rsa);
    }
}
