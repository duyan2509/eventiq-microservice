namespace Eventiq.Contracts;

public record IssueTicketsCommand
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid SessionId { get; init; }
    public List<IssueTicketSeat> Seats { get; init; } = [];
}

public record IssueTicketSeat
{
    public Guid SeatId { get; init; }
    public string SeatLabel { get; init; } = string.Empty;
    public string LegendName { get; init; } = string.Empty;
    public decimal Price { get; init; }
}
