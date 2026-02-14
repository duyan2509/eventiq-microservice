namespace Eventiq.EventService.Domain.Entity;

public class Legend : BaseEntity
{
    public string Name { get; set; }
    public string? Color { get; set; }
    public int Price { get; set; }
    public Guid EventId { get; set; }
    public virtual Event Event { get; set; }
}