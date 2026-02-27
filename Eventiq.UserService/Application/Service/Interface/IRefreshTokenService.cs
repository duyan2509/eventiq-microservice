using Eventiq.UserService.Domain.Enums;
using Eventiq.UserService.Model;

namespace Eventiq.UserService.Application.Service;

public interface IRefreshTokenService
{
    Task<string> GenerateRefreshToken(
        string userId,
        AppRoles currentRole
    );

    bool ValidateRefreshToken(RefreshTokenModel? refreshToken);
    Task<RefreshTokenModel?> GetRefreshTokenModel(string refreshToken);


    Task RevokeRefreshToken(string refreshToken);
    Task<RefreshTokenModel?> GetRefreshTokenModelByUserId(Guid userId);

}