namespace Eventiq.EventService.Infrastructure.Persistence.ReadModel;

public class SessionModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Guid EventId { get; set; }
    public Guid ChartId { get; set; }
    public string ChartName { get; set; }
}
