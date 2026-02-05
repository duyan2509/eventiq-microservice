using Eventiq.OrganizationService.Domain.Enum;

namespace Eventiq.OrganizationService.Domain.Entity;
public class Invitation : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public virtual Organization Organization { get; set; }
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public InvitationStatus  Status { get; set; }
    public Guid PermissionId { get; set; }   
    public Permission Permission { get; set; }
}