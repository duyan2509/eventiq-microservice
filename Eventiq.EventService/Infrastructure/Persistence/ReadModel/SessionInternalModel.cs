namespace Eventiq.EventService.Infrastructure.Persistence.ReadModel;

public class SessionInternalModel
{
    public Guid SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public Guid OrgId { get; set; }
}
