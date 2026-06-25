namespace Eventiq.Contracts;

public record TicketsIssued
{
    public Guid OrderId { get; init; }
}
