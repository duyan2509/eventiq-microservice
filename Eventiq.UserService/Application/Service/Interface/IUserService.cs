using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Domain.Enums;
using Eventiq.UserService.Model;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Eventiq.UserService.Application.Service;

public interface IUserService
{
    Task<LoginResponse> Login(LoginDto dto);
    Task<RefreshResponse> Refresh(string refreshToken);
    Task Logout(string refreshToken);
    Task<bool> Register(RegisterDto dto);
    Task<UserDto> GetMe(Guid userId, string role);
    Task<PaginatedResult<UserResponse>> GetAllUsers(int page, int size, string? email );


    Task<bool> BanUser(Guid adminId, Guid userId, BanUserRequest dto);
    Task<bool> UnbanUser(Guid adminId, Guid userId);
    
    Task<UserDto> ChangePassword(Guid userId, ChangePasswordRequest dto);
    Task<SwitchRoleRepsponse> SwitchRole(Guid userId, AppRoles role, Guid? OrganizationId);
    
}





