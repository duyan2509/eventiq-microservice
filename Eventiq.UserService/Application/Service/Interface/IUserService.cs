using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Domain.Enums;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Eventiq.UserService.Application.Service;

public interface IUserService
{
    Task<LoginResponse> Login(LoginDto dto);
    Task<RefreshResponse> Refresh(string refreshToken);
    void Logout(string refreshToken);
    Task<RegisterDto> Register(RegisterDto dto);
    Task<UserDto> GetMe(Guid userId);
    Task<PaginatedResult<UserDto>> GetAllUsers(int page, int size, string email);
    Task<bool> BanUser(Guid adminId, Guid userId, BanUserRequest dto);
    Task<bool> UnbanUser(Guid adminId, Guid userId);
    Task<UserDto> ChangePassword(Guid userId, ChangePasswordRequest dto);
    Task<SwitchRoleRepsponse> SwitchRole(Guid userId, AppRoles role);
}





