namespace Eventiq.Contracts;

public record CheckoutSessionExpired
{
    public Guid OrderId { get; init; }
}
