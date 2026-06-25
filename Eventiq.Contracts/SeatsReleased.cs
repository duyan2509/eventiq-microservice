namespace Eventiq.Contracts;

public record SeatsReleased
{
    public Guid OrderId { get; init; }
}
