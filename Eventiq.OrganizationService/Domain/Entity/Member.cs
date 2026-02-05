namespace Eventiq.OrganizationService.Domain.Entity;
public class Member: BaseEntity
{
    public Guid? UserId { get; set; }
    public string Email { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid PermissionId { get; set; }
    public virtual Organization Organization { get; set; }
    public virtual Permission Permission { get; set; }
}

