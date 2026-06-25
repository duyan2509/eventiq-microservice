using Eventiq.Contracts;
using Eventiq.EventService.Application.Service.Interface;
using MassTransit;

namespace Eventiq.EventService.Consumers;

public class IssueTicketsConsumer : IConsumer<IssueTicketsCommand>
{
    private readonly ITicketService _ticketService;
    private readonly ILogger<IssueTicketsConsumer> _logger;

    public IssueTicketsConsumer(ITicketService ticketService, ILogger<IssueTicketsConsumer> logger)
    {
        _ticketService = ticketService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IssueTicketsCommand> context)
    {
        var msg = context.Message;
        _logger.LogInformation("IssueTickets: OrderId={OrderId}, Seats={Count}", msg.OrderId, msg.Seats.Count);

        var seats = msg.Seats
            .Select(s => (s.SeatId, s.SeatLabel, s.LegendName, s.Price))
            .ToList();

        await _ticketService.IssueAsync(msg.OrderId, msg.SessionId, seats);

        await context.Publish(new TicketsIssued { OrderId = msg.OrderId });
        _logger.LogInformation("TicketsIssued published for OrderId={OrderId}", msg.OrderId);
    }
}
