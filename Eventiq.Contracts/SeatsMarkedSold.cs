namespace Eventiq.Contracts;

public record SeatsMarkedSold
{
    public Guid OrderId { get; init; }
}
