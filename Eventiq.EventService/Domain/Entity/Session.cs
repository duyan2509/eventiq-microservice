namespace Eventiq.EventService.Domain.Entity;

public class Session : BaseEntity
{
    public string Name { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Guid EventId { get; set; }
    public virtual Event Event { get; set; }
    public Guid ChartId { get; set; }
    public virtual Chart Chart { get; set; }
    public void ValidateSessionTime(Session session)
    {
        if (StartTime == null || EndTime == null)
            throw new BusinessException("Event must have StartTime and EndTime before adding sessions.");

        if (session.StartTime >= session.EndTime)
            throw new BusinessException("Session StartTime must be less than EndTime.");

        
    }
}