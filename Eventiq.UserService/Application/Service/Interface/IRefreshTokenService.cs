namespace Eventiq.UserService.Application.Service;

public interface IRefreshTokenService
{
    string GenerateRefreshToken(
        string userId
    );

    bool ValidateRefreshToken(
        string refreshToken,
        out string userId
    );

    void RevokeRefreshToken(string refreshToken);
}