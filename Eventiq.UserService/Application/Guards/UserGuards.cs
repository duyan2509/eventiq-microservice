using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Enums;
using Eventiq.UserService.Model;

namespace Eventiq.UserService.Guards;

public static class UserGuards
{
    public static void EnsureExist(LoginUserModel user)
    {
        if(user == null)
            throw new NotFoundException("User not found");
    }
    public static void EnsureActive(LoginUserModel user)
    {
        if (user.IsBanned)
            throw new ForbiddenException( $"Account with email {user.Email} is banned");
    }
    public static void EnsureAdmin(LoginUserModel user)
    {
        if(!user.Roles.Contains(AppRoles.Admin.ToString()))
            throw new ForbiddenException( $"Account with email {user.Email} has no admin permission");
    }
    public static void EnsureHasRole(LoginUserModel user, AppRoles role)
    {
        if(!user.Roles.Contains(role.ToString()))
            throw new ForbiddenException( $"Account with email {user.Email} has no {role.ToString()} permission");
    }
}
