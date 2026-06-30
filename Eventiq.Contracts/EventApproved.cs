namespace Eventiq.Contracts;

public record EventApproved
{
    public Guid EventId { get; init; }
    public DateTime ApprovedAt { get; init; }
}
