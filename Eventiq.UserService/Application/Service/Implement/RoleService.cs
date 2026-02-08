using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Enums;
using Eventiq.UserService.Domain.Repositories;

namespace Eventiq.UserService.Application.Service;

public class RoleService:IRoleService
{
    private readonly ILogger<RoleService> _logger;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;

    public RoleService(ILogger<RoleService> logger, IRoleRepository roleRepository, IUserRepository userRepository, IUserRoleRepository userRoleRepository)
    {
        _logger = logger;
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
    }

    public async Task EnsureOrgRoleAsync(Guid userId, Guid organizationId)
    {
        var user = await _userRepository.GetUserById(userId);
        if (user == null)
            throw new NotFoundException($"User with id {userId} not found");
        if (!user.Roles.Contains(nameof(AppRoles.Organization)))
        {
            var orgRole = await _roleRepository.GetRoleByName(nameof(AppRoles.Organization));
            if (orgRole == null)
                throw new NotFoundException($"Organization role is not found");
            var useRole = await _userRoleRepository.GetUserRoleByRoleIdNOrgId(orgRole.Id,  organizationId);
            if (useRole == null)
                await _userRoleRepository.AddUserRole(new UserRole()
                {
                    UserId = userId,
                    RoleId = orgRole.Id,
                    OrganizationId = organizationId
                });
        }
    }
}