namespace Eventiq.EventService.Domain.Entity;

public class Submission : BaseEntity
{
    public Guid EventId { get; set; }
    public virtual Event Event { get; set; }
    public string AdminEmail { get; set; }
    public Guid AdminId { get; set; }
    public string Message { get; set; }
    public EventStatus Status { get; set; }
}
