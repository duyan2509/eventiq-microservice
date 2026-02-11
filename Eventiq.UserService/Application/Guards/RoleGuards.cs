using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Enums;
using Eventiq.UserService.Model;

namespace Eventiq.UserService.Guards;
public static class RoleGuards
{
    public static string ResolveActiveRole(LoginUserModel user)
    {
        var priority = new[]
        {
            AppRoles.Admin,
            AppRoles.User,
            AppRoles.Staff,
            AppRoles.Organization
        };

        return priority
            .Select(r => r.ToString())
            .First(r => user.Roles.Contains(r));
    }

    public static void EnsureUserRoleNotFound(UserRole userRole)
    {
        if(userRole!=null)
            throw new BusinessException("User already has this role");
    }
    public static void EnsureExist(Role? role)
    {
        if(role==null)
            throw new NotFoundException($"Role {role.Name} is not found");
    }

    public static void EnsureUserRoleExist(UserRole? useRole)
    {
        if(useRole==null)
            throw new NotFoundException($"User role is not found");
    }
}
