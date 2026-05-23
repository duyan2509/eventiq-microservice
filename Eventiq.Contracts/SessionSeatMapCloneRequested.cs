namespace Eventiq.Contracts;

public record SessionSeatMapCloneRequested
{
    public Guid SessionId { get; init; }
    public Guid ChartId { get; init; }
    public Guid EventId { get; init; }
}
