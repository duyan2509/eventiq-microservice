namespace Eventiq.UserService.Application.Dto;

public class SwitchRoleRequest
{
    public Guid OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
}