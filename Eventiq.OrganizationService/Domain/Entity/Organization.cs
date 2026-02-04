namespace Eventiq.OrganizationService.Domain.Entity;

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
