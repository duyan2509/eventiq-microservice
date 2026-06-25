namespace Eventiq.Contracts;

public record MarkSeatsSoldCommand
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public List<Guid> SeatIds { get; init; } = [];
}
