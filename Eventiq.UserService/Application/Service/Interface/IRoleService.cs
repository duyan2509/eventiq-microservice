namespace Eventiq.UserService.Application.Service;

public interface IRoleService
{
    Task EnsureOrgRoleAsync(Guid userId, Guid organizationId);
}