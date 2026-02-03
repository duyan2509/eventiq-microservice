using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Model;

namespace Eventiq.UserService.Domain.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshTokenModel> AddRefreshToken(RefreshToken refreshToken);
    Task  RemoveRefreshToken(string  refreshToken);
    Task<RefreshTokenModel?> GetRefreshToken(string refreshToken);
}

