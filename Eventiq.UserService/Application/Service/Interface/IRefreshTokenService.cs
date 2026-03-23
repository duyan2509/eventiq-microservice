using Eventiq.UserService.Model;

namespace Eventiq.UserService.Application.Service;

public interface IRefreshTokenService
{
    Task<string> GenerateRefreshToken(
        string userId,
        Guid? organizationId = null
    );

    bool ValidateRefreshToken(RefreshTokenModel? refreshToken);
    Task<RefreshTokenModel?> GetRefreshTokenModel(string refreshToken);


    Task RevokeRefreshToken(string refreshToken);
    Task<RefreshTokenModel?> GetRefreshTokenModelByUserId(Guid userId);

}