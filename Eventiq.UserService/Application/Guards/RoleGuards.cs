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
}
