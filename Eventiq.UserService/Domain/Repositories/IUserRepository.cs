using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Enums;
using Eventiq.UserService.Model;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Eventiq.UserService.Domain.Repositories;

public interface IUserRepository
{
    Task<LoginUserModel> GetUserByEmail(string email);
    Task<LoginUserModel> GetUserById(Guid userId);
    Task AddUser(User user);
    Task UpdateUser(User user);
    Task<User?> GetTrackingUserById(Guid userId);
    Task<PaginatedResult<User>> GetAllUsers(int page, int size, string enail);

}

