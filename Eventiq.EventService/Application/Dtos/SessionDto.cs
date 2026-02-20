namespace Eventiq.EventService.Dtos;

public class UpdateSessionDto
{
    public string? Name { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public Guid? ChartId { get; set; }
}

public class CreateSessionDto
{
    public required string Name { get; set; }
    public required DateTime StartTime { get; set; }
    public required DateTime EndTime { get; set; }
    public required Guid ChartId { get; set; }
}

public class SessionResponse
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required DateTime StartTime { get; set; }
    public required DateTime EndTime { get; set; }
    public required Guid ChartId { get; set; }
    public string ChartName { get; set; }

}
