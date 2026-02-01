using Eventiq.UserService.Application.Dto;

namespace Eventiq.UserService.Application.Service;

public interface IUserService
{
    Task<LoginResponse> Login(LoginDto dto);
    Task<RefreshResponse> Refresh(string refreshToken);
    void Logout(string refreshToken);
}