using Eventiq.UserService.Domain.Enums;

namespace Eventiq.UserService.Application.Dto;

public class SwitchRoleRequest
{
    public AppRoles Role { get; set; }
    public Guid OrganizationId { get; set; }
}