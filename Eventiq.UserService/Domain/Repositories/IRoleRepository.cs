using Eventiq.UserService.Domain.Entity;

namespace Eventiq.UserService.Domain.Repositories;

public interface IRoleRepository
{
    Task<Role?> GetRoleByName(string name);
}