using System.Security.Cryptography;

namespace Eventiq.UserService.Application.Service;

public class RefreshTokenService:IRefreshTokenService
{
    private static readonly Dictionary<string, string> _store = new(); // temp for set up test no db

    public string GenerateRefreshToken(string userId)
    {
        var token = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64)
        );

        _store[token] = userId;
        return token;
    }

    public bool ValidateRefreshToken(
        string refreshToken,
        out string userId
    )
    {
        return _store.TryGetValue(refreshToken, out userId);
    }

    public void RevokeRefreshToken(string refreshToken)
    {
        _store.Remove(refreshToken);
    }
}