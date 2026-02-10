namespace Eventiq.OrganizationService.Domain.Entity;
public class Permission : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public virtual Organization Organization { get; set; }
    public bool IsDesigner { get; set; } = false;
}

