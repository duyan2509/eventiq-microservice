namespace Eventiq.Contracts;

public record PaymentCompleted
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid SessionId { get; init; }
    public List<PaymentCompletedSeat> Seats { get; init; } = [];
}

public record PaymentCompletedSeat
{
    public Guid SeatId { get; init; }
    public string SeatLabel { get; init; } = string.Empty;
    public string LegendName { get; init; } = string.Empty;
    public decimal Price { get; init; }
}
