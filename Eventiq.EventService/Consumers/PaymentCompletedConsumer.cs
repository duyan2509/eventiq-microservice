using Eventiq.Contracts;
using Eventiq.EventService.Application.Service.Interface;
using MassTransit;

namespace Eventiq.EventService.Consumers;

public class PaymentCompletedConsumer : IConsumer<PaymentCompleted>
{
    private readonly ITicketService _ticketService;
    private readonly ILogger<PaymentCompletedConsumer> _logger;

    public PaymentCompletedConsumer(
        ITicketService ticketService,
        ILogger<PaymentCompletedConsumer> logger)
    {
        _ticketService = ticketService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentCompleted> context)
    {
        var msg = context.Message;

        // SeatService consumer handles marking seats Sold — this consumer only issues tickets
        var seats = msg.Seats.Select(s => (s.SeatId, s.SeatLabel, s.LegendName, s.Price)).ToList();
        await _ticketService.IssueAsync(msg.OrderId, msg.SessionId, seats);

        _logger.LogInformation("Tickets issued for order {OrderId}", msg.OrderId);
    }
}
