namespace Eventiq.UserService.Application.Service;

public interface IRoleService
{
    Task EnsureOrgRoleAsync(Guid userId, Guid organizationId);
    Task AssignOrgStaffRoleAsync(Guid userId, Guid organizationId);
    Task InvokeOrgStaffRoleAsync(Guid userId, Guid organizationId);
}