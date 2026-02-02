using Eventiq.UserService.Model;

namespace Eventiq.UserService.Application.Service;

public interface IRefreshTokenService
{
    Task<string> GenerateRefreshToken(
        string userId
    );

    bool ValidateRefreshToken(RefreshTokenModel? refreshToken);
    Task<RefreshTokenModel?> GetRefreshTokenModel(string refreshToken);


    void RevokeRefreshToken(string refreshToken);
}