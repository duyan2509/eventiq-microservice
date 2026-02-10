using Eventiq.OrganizationService.Domain.Enum;

namespace Eventiq.OrganizationService.Dtos;


public class InvitationDto
{
    public Guid OrganizationId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public Guid PermissionId { get; set; }   
}
public class InviationResponse
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string  Status { get; set; }
    public Guid PermissionId { get; set; }   
    public string PermissionName { get; set; } = string.Empty;
}
public class AcceptDto
{
    public Guid PermissionId { get; set; }
}