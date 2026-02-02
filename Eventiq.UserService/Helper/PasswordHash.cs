using System.Security.Cryptography;
using System.Text;

namespace Eventiq.UserService.Helper;

public static class PasswordHash
{
    public static string SHA256Hash(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}