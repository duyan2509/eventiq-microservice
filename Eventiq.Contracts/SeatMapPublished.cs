namespace Eventiq.Contracts;

public record SeatMapPublished
{
    public Guid SeatMapId { get; init; }
    public Guid ChartId { get; init; }
    public Guid EventId { get; init; }
    public Guid OrganizationId { get; init; }
    public int TotalSeats { get; init; }
}
