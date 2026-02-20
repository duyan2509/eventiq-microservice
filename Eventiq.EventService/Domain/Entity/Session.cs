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
    public void ValidateSessionTime()
    {
        if (StartTime == null || EndTime == null)
            throw new BusinessException("Event must have StartTime and EndTime before adding sessions.");

        if (StartTime >= EndTime)
            throw new BusinessException("Session StartTime must be less than EndTime.");
        if (StartTime < DateTime.UtcNow)
            throw new BusinessException("Session cannot start in the past.");
        
    }
    public void Update(
        string? name,
        DateTime? startTime,
        DateTime? endTime ,
        Guid? chartId)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name;

        if (startTime.HasValue)
            StartTime = startTime.Value;

        if (endTime.HasValue)
            EndTime = endTime.Value;
        if(chartId != Guid.Empty)
            ChartId = chartId.Value;
        ValidateSessionTime();
    }
}