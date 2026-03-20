namespace Eventiq.Contracts;

public record ChartDeleted
{
    public Guid ChartId { get; init; }
    public Guid EventId { get; init; }
    public Guid OrganizationId { get; init; }
}
