namespace Eventiq.EventService.Domain.Entity;

public class Chart : BaseEntity
{
    public string Name { get; set; }
    public Guid EventId { get; set; }
    public virtual Event Event { get; set; }
    public ICollection<Session> Sessions { get; set; }=new List<Session>();
}
