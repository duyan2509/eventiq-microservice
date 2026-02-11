using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Enums;
using Eventiq.UserService.Domain.Repositories;
using Eventiq.UserService.Guards;

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
        UserGuards.EnsureExist(user);
        var orgRole = await _roleRepository.GetRoleByName(nameof(AppRoles.Organization));
        RoleGuards.EnsureExist(orgRole);
        var useRole = await _userRoleRepository.GetUserRoleByRoleIdNOrgId(orgRole.Id,  organizationId);
        RoleGuards.EnsureUserRoleNotFound(useRole);
        await _userRoleRepository.AddUserRole(new UserRole()
        {
            UserId = userId,
            RoleId = orgRole.Id,
            OrganizationId = organizationId
        });
    }

    public async Task AssignOrgStaffRoleAsync(Guid userId, Guid organizationId)
    {
        var user = await _userRepository.GetUserById(userId);
        UserGuards.EnsureExist(user);
        var orgRole = await _roleRepository.GetRoleByName(nameof(AppRoles.Staff));
        RoleGuards.EnsureExist(orgRole);
        var useRole = await _userRoleRepository.GetUserRoleByOrgIdUserIdRoleIdAsync(organizationId,userId,orgRole.Id);
        RoleGuards.EnsureUserRoleNotFound(useRole);
        await _userRoleRepository.AddUserRole(new UserRole()
        {
            UserId = userId,
            RoleId = orgRole.Id,
            OrganizationId = organizationId
        });
    }

    public async Task InvokeOrgStaffRoleAsync(Guid userId, Guid organizationId)
    {
        var user = await _userRepository.GetUserById(userId);
        UserGuards.EnsureExist(user);
        var orgRole = await _roleRepository.GetRoleByName(nameof(AppRoles.Staff));
        RoleGuards.EnsureExist(orgRole);
        var useRole = await _userRoleRepository.GetUserRoleByOrgIdUserIdRoleIdAsync(organizationId,userId,orgRole.Id);
        RoleGuards.EnsureUserRoleExist(useRole);
        await _userRoleRepository.RemoveUserRole(useRole);
    }
}