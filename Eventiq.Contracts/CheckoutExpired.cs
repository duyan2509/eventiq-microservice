namespace Eventiq.Contracts;

public record CheckoutExpired
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public List<Guid> SeatIds { get; init; } = [];
}
