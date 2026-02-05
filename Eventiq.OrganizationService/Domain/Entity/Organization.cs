namespace Eventiq.OrganizationService.Domain.Entity;

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public virtual ICollection<Member>  Members { get; set; } = new List<Member>();
    public Guid OwnerId { get; set; }
    public string OwnerEmail { get; set; } = string.Empty;
    public virtual ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}

