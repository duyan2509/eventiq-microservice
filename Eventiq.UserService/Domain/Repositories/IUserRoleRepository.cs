using Eventiq.UserService.Domain.Entity;

namespace Eventiq.UserService.Domain.Repositories;

public interface IUserRoleRepository
{
    Task AddUserRole(UserRole userRole);
    Task<UserRole?> GetUserRoleByRoleIdNOrgId(Guid roleId, Guid orgId);
    Task<UserRole?> GetUserRoleByOrgIdUserIdRoleIdAsync(Guid orgId, Guid userId, Guid roleId);
    Task  RemoveUserRole(UserRole userRole);
}