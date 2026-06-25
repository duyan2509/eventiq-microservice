namespace Eventiq.Contracts;

public record BookingInitiated
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid SessionId { get; init; }
    public List<Guid> SeatIds { get; init; } = [];
}
